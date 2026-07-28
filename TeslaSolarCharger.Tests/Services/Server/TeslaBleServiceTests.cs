using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class TeslaBleServiceTests : TestBase
{
    private const string TestVin = "TESTVIN123456789A";
    private const string ConfiguredBleUrl = "http://blecontainer:7210";

    private readonly RecordingHttpMessageHandler _handler = new();

    public TeslaBleServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        //TeslaBleService must ask the factory for its clients, otherwise every call would create a new connection pool.
        Mock.Mock<IHttpClientFactory>()
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    [Fact]
    public async Task BuildsCommandUrlFromConfiguredBaseUrl()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.OK, "{\"Success\":true,\"ResultMessage\":\"done\"}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.StartCharging(TestVin);

        Assert.True(result.Success);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/Command/ExecuteCommand", request.RequestUri!.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        Assert.Equal(TestVin, query["vin"]);
        Assert.Equal("charging-start", query["command"]);
        //Only vcsec commands carry a domain, everything else must not send the parameter at all.
        Assert.Null(query["domain"]);
    }

    [Theory]
    //A configured url may or may not already end with a slash and/or the api segment.
    [InlineData("http://blecontainer:7210", "http://blecontainer:7210/api/Command/ExecuteCommand")]
    [InlineData("http://blecontainer:7210/", "http://blecontainer:7210/api/Command/ExecuteCommand")]
    [InlineData("http://blecontainer:7210/api", "http://blecontainer:7210/api/Command/ExecuteCommand")]
    [InlineData("http://blecontainer:7210/api/", "http://blecontainer:7210/api/Command/ExecuteCommand")]
    public async Task NormalizesConfiguredBaseUrl(string configuredUrl, string expectedUrlWithoutQuery)
    {
        SetupBleCar(configuredUrl);
        _handler.RespondWith(HttpStatusCode.OK, "{\"Success\":true}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        await service.StartCharging(TestVin);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(expectedUrlWithoutQuery, request.RequestUri!.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public async Task WakeUpCarUsesVcsecDomain()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.OK, "{\"Success\":true}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        await service.WakeUpCar(TestVin);

        var request = Assert.Single(_handler.Requests);
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
        Assert.Equal("wake", query["command"]);
        Assert.Equal("vcsec", query["domain"]);
    }

    [Fact]
    public async Task MissingBaseUrlIsReportedAsConfigurationErrorWithoutHttpCall()
    {
        SetupBleCar(configuredBleUrl: null);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.StartCharging(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.TscConfiguration, result.ErrorType);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task NonSuccessStatusCodeIsReportedAsFailure()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.InternalServerError, "something broke");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.StartCharging(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task UnparsableResponseIsReportedAsFailure()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.OK, "null");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.StartCharging(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task TimeoutIsReportedWithAnExplicitMessage()
    {
        SetupBleCar();
        //This is what HttpClient throws once the per call CancellationTokenSource fires.
        _handler.ThrowOnSend(new TaskCanceledException());

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.StartCharging(TestVin);

        Assert.False(result.Success);
        //The message ends up in the UI, so the useless "A task was canceled." must not be surfaced.
        Assert.NotNull(result.ResultMessage);
        Assert.Contains("timed out", result.ResultMessage);
        Assert.DoesNotContain("A task was canceled", result.ResultMessage);
    }

    [Fact]
    public async Task NonCancellationExceptionKeepsItsOwnMessage()
    {
        SetupBleCar();
        _handler.ThrowOnSend(new HttpRequestException("Connection refused"));

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.StartCharging(TestVin);

        Assert.False(result.Success);
        Assert.Equal("Connection refused", result.ResultMessage);
    }

    [Fact]
    public async Task SetAmpSendsAmpsAsParameter()
    {
        SetupBleCar(requestedCurrent: 10);
        _handler.RespondWith(HttpStatusCode.OK, "{\"Success\":true}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        await service.SetAmp(TestVin, 12);

        var request = Assert.Single(_handler.Requests);
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
        Assert.Equal("charging-set-amps", query["command"]);
        Assert.Equal("[\"12\"]", _handler.RequestBodies.Single());
    }

    [Theory]
    //Tesla does not apply the change immediately when crossing the 5A boundary, so the command is sent twice.
    [InlineData(10, 3, 2)]
    [InlineData(3, 10, 2)]
    [InlineData(10, 12, 1)]
    [InlineData(3, 4, 1)]
    public async Task SetAmpDoubleSendsOnlyWhenCrossingTheFiveAmpBoundary(int initialAmps, int newAmps, int expectedRequests)
    {
        SetupBleCar(requestedCurrent: initialAmps);
        _handler.RespondWith(HttpStatusCode.OK, "{\"Success\":true}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        await service.SetAmp(TestVin, newAmps);

        Assert.Equal(expectedRequests, _handler.Requests.Count);
    }

    [Fact]
    public async Task DownloadLogsRefusesUrlsThatAreNotConfiguredOnAnyCar()
    {
        SetupBleCar();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        //TSC must not be usable as a request proxy for arbitrary hosts.
        var stream = await service.DownloadLogs("http://someone-elses-host:7210");

        Assert.Null(stream);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task DownloadLogsUsesConfiguredUrl()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.OK, "log content");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var stream = await service.DownloadLogs(ConfiguredBleUrl);

        Assert.NotNull(stream);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal("/api/Debug/DownloadInMemoryLogs", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CommandsOfCarsWithoutBleDebugDoNotRequestDebugOutput()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.OK, "{\"Success\":true}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        await service.StartCharging(TestVin);

        var request = Assert.Single(_handler.Requests);
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
        Assert.Null(query["useDebug"]);
    }

    [Fact]
    public async Task CommandsOfCarsWithBleDebugRequestDebugOutput()
    {
        var car = SetupBleCar();
        //Debug is enabled per car on the support page so a single car can be troubleshooted.
        car.UseBleDebug = true;
        _handler.RespondWith(HttpStatusCode.OK, "{\"Success\":true}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        await service.StartCharging(TestVin);

        var request = Assert.Single(_handler.Requests);
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
        Assert.Equal("true", query["useDebug"]);
    }

    [Fact]
    public async Task BeaconScanBuildsGetUrlFromConfiguredBaseUrl()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.OK, "{\"Success\":true,\"ResultMessage\":\"{\\\"beaconFound\\\":true}\"}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.GetBeaconScanResult(TestVin);

        Assert.True(result.Success);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/Command/BeaconScan", request.RequestUri!.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        Assert.Equal(TestVin, query["vin"]);
    }

    [Fact]
    public async Task BeaconScanReportsNotFoundAsFailure()
    {
        SetupBleCar();
        //Old BLE containers do not know the endpoint yet: the caller falls back to the legacy presence detection.
        _handler.RespondWith(HttpStatusCode.NotFound, string.Empty);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.GetBeaconScanResult(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task BeaconScanMissingBaseUrlIsReportedWithoutHttpCall()
    {
        SetupBleCar(configuredBleUrl: null);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.GetBeaconScanResult(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.TscConfiguration, result.ErrorType);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task BeaconScanTimeoutIsReportedWithAnExplicitMessage()
    {
        SetupBleCar();
        _handler.ThrowOnSend(new TaskCanceledException());

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var result = await service.GetBeaconScanResult(TestVin);

        Assert.False(result.Success);
        Assert.NotNull(result.ResultMessage);
        Assert.Contains("timed out", result.ResultMessage);
    }

    [Fact]
    public async Task VersionCompatibilityAcceptsTheExpectedVersion()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.OK, "{\"Value\":\"2.37.0\"}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var error = await service.CheckBleApiVersionCompatibility(ConfiguredBleUrl);

        Assert.Null(error);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal("/api/Hello/TscVersionCompatibility", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task VersionCompatibilityReportsOutdatedContainerOnNotFound()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.NotFound, string.Empty);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var error = await service.CheckBleApiVersionCompatibility(ConfiguredBleUrl);

        Assert.NotNull(error);
        Assert.Contains("not up to date", error);
    }

    [Fact]
    public async Task VersionCompatibilityReportsMismatchingVersion()
    {
        SetupBleCar();
        _handler.RespondWith(HttpStatusCode.OK, "{\"Value\":\"2.35.0\"}");

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaBleService>();
        var error = await service.CheckBleApiVersionCompatibility(ConfiguredBleUrl);

        Assert.NotNull(error);
        Assert.Contains("incompatible version", error);
    }

    private DtoCar SetupBleCar(string? configuredBleUrl = ConfiguredBleUrl, int requestedCurrent = 16)
    {
        var dtoCar = new DtoCar
        {
            Id = 1,
            Vin = TestVin,
            UseBle = true,
            ShouldBeManaged = true,
            BleApiBaseUrl = configuredBleUrl,
            ChargerRequestedCurrent = new(DateTimeOffset.MinValue, requestedCurrent),
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        return dtoCar;
    }

    /// <summary>
    /// Records every request that reaches the transport so the tests can assert on urls, methods and bodies without
    /// touching the network.
    /// </summary>
    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string _responseContent = string.Empty;
        private Exception? _exceptionToThrow;

        public List<HttpRequestMessage> Requests { get; } = new();

        public List<string> RequestBodies { get; } = new();

        public void RespondWith(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _responseContent = content;
            _exceptionToThrow = null;
        }

        public void ThrowOnSend(Exception exception) => _exceptionToThrow = exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content == default
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (_exceptionToThrow != default)
            {
                throw _exceptionToThrow;
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent),
            };
        }
    }
}
