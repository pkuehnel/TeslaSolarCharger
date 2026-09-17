using Newtonsoft.Json;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Resources;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;


/// <summary>
/// Covers how a failed BLE request is turned into something the user can act on. Getting this wrong sends people
/// looking for the wrong problem, e.g. re-pairing a key for a car that simply is not at home.
/// </summary>
public class TeslaBleServiceTests : TestBase
{
    private const string TestVin = "TESTVIN123456789A";

    public TeslaBleServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void ReadableChargeStateMeansEverythingWorks()
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = true, });

        Assert.Equal(BleConnectionTestResultType.Success, result);
    }

    [Theory]
    [InlineData(BleCommandOutcome.AdapterNotFound)]
    [InlineData(BleCommandOutcome.AdapterUnavailable)]
    [InlineData(BleCommandOutcome.WorkerError)]
    [InlineData(BleCommandOutcome.WorkerTimeout)]
    [InlineData(BleCommandOutcome.InvalidRequest)]
    public void LocalProblemsAreReportedAsContainerProblem(BleCommandOutcome outcome)
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = false, Outcome = outcome, });

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void SleepingCarNeedsNoFurtherChecks()
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarAsleep,
        });

        Assert.Equal(BleConnectionTestResultType.CarAsleep, result);
    }

    [Theory]
    [InlineData(BleCommandOutcome.CarAbsent)]
    [InlineData(BleCommandOutcome.LinkFailed)]
    [InlineData(BleCommandOutcome.CarRefused)]
    [InlineData(null)]
    public void UnclearOutcomesAreNarrowedDownFurther(BleCommandOutcome? outcome)
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = false, Outcome = outcome, });

        Assert.Null(result);
    }

    [Fact]
    public void CarThatWasNotHeardIsReportedAsNotFound()
    {
        var presence = CreatePresence(heard: false);

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.CarNotFound, result);
    }

    [Fact]
    public void WarmingUpScanNeverDeclaresACarAway()
    {
        var presence = CreatePresence(heard: false);
        presence.WarmingUp = true;

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Null(result);
    }

    [Fact]
    public void HeardCarIsNarrowedDownFurther()
    {
        var presence = CreatePresence(heard: true);

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Null(result);
    }

    [Fact]
    public void StoppedScannerCarriesNoPresenceInformation()
    {
        var presence = CreatePresence(heard: false);
        presence.ScannerRunning = false;

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void UnreachableContainerIsNoStatementAboutTheCar()
    {
        var presence = CreatePresence(heard: false);
        presence.ErrorMessage = "container not reachable";

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void PresentCarWithoutBodyControllerAnswerMeansMissingKey()
    {
        var bodyControllerState = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.LinkFailed,
            ResultMessage = "failed to connect: vehicle rejected request: your public key has not been paired with the vehicle",
        };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.KeyNotPaired, result);
    }

    [Fact]
    public void CarThatLeftBetweenTheRequestsIsNotBlamedOnTheKey()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = false, Outcome = BleCommandOutcome.CarAbsent, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.CarNotFound, result);
    }

    [Fact]
    public void AnsweringBodyControllerOfASleepingCarMeansTheKeyWorks()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.CarAsleep, result);
    }

    [Fact]
    public void AwakeCarWithWorkingKeyButFailedChargeStateStaysUnknown()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: true);

        Assert.Equal(BleConnectionTestResultType.Unknown, result);
    }

    public static TheoryData<string, string> CommandsWithoutParameters => new()
    {
        { nameof(TeslaBleService.OpenChargePortDoor), "charge-port-open" },
        { nameof(TeslaBleService.FlashLights), "flash-lights" },
        { nameof(TeslaBleService.StartCharging), "charging-start" },
        { nameof(TeslaBleService.StopCharging), "charging-stop" },
        { nameof(TeslaBleService.GetBodyControllerState), "body-controller-state" },
    };

    [Theory]
    [MemberData(nameof(CommandsWithoutParameters))]
    public async Task CommandWithoutParametersIsSentToTheContainerOfTheCar(string methodName, string expectedCommand)
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, });
        var service = Mock.Create<TeslaBleService>();

        var result = await InvokeCommand(service, methodName);

        Assert.True(result.Success);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://ble-container:7210/api/" + BleApiRoutes.ExecuteCommand, request.Uri.GetLeftPart(UriPartial.Path));
        var query = HttpUtility.ParseQueryString(request.Uri.Query);
        Assert.Equal(TestVin, query[BleApiRoutes.VinQueryParam]);
        Assert.Equal(expectedCommand, query[BleApiRoutes.CommandQueryParam]);
        Assert.Null(query[BleApiRoutes.DomainQueryParam]);
        Assert.Null(query[BleApiRoutes.AdapterQueryParam]);
        Assert.Null(query[BleApiRoutes.KeepWarmSecondsQueryParam]);
        Assert.Equal("[]", request.Body);
    }

    [Fact]
    public async Task OpenChargePortDoorUsesTheAdapterPinnedToTheCar()
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, }, adapter: "AA:BB:CC:DD:EE:FF");
        var service = Mock.Create<TeslaBleService>();

        await service.OpenChargePortDoor(TestVin);

        var query = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Uri.Query);
        Assert.Equal("AA:BB:CC:DD:EE:FF", query[BleApiRoutes.AdapterQueryParam]);
    }

    [Fact]
    public async Task OpenChargePortDoorPassesTheRefusalOfTheCarOn()
    {
        SetupContainer(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = "charge port door is already open",
        });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.OpenChargePortDoor(TestVin);

        Assert.False(result.Success);
        Assert.Equal(BleCommandOutcome.CarRefused, result.Outcome);
        Assert.Equal("charge port door is already open", result.CarErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OpenChargePortDoorWithoutBleUrlIsAConfigurationError(string? bleApiBaseUrl)
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, }, bleApiBaseUrl: bleApiBaseUrl);
        var service = Mock.Create<TeslaBleService>();

        var result = await service.OpenChargePortDoor(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.TscConfiguration, result.ErrorType);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OpenChargePortDoorOnAFailingContainerIsUnsuccessful()
    {
        SetupContainer(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom"),
        }));
        var service = Mock.Create<TeslaBleService>();

        var result = await service.OpenChargePortDoor(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task OpenChargePortDoorOnAnUnreachableContainerIsUnsuccessful()
    {
        SetupContainer(new CapturingHandler(_ => throw new HttpRequestException("No route to host")));
        var service = Mock.Create<TeslaBleService>();

        var result = await service.OpenChargePortDoor(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
        Assert.Equal("No route to host", result.ResultMessage);
    }

    [Fact]
    public async Task OpenChargePortDoorOnAnUnparsableAnswerIsUnsuccessful()
    {
        SetupContainer(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null"),
        }));
        var service = Mock.Create<TeslaBleService>();

        var result = await service.OpenChargePortDoor(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task OpenChargePortDoorForAnUnknownCarThrows()
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, });
        var service = Mock.Create<TeslaBleService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenChargePortDoor("UNKNOWNVIN1234567"));
        Assert.Empty(handler.Requests);
    }

    private static Task<DtoBleCommandResult> InvokeCommand(TeslaBleService service, string methodName) => methodName switch
    {
        nameof(TeslaBleService.OpenChargePortDoor) => service.OpenChargePortDoor(TestVin),
        nameof(TeslaBleService.FlashLights) => service.FlashLights(TestVin),
        nameof(TeslaBleService.StartCharging) => service.StartCharging(TestVin),
        nameof(TeslaBleService.StopCharging) => service.StopCharging(TestVin),
        nameof(TeslaBleService.GetBodyControllerState) => service.GetBodyControllerState(TestVin),
        _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null),
    };

    private CapturingHandler SetupContainer(DtoBleCommandResult containerResult,
        string? bleApiBaseUrl = "http://ble-container:7210", string? adapter = null) =>
        SetupContainer(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(containerResult)),
        }), bleApiBaseUrl, adapter);

    private CapturingHandler SetupContainer(CapturingHandler handler,
        string? bleApiBaseUrl = "http://ble-container:7210", string? adapter = null)
    {
        var car = new DtoCar
        {
            Vin = TestVin,
            UseBle = true,
            BleApiBaseUrl = bleApiBaseUrl,
            BleAdapterAddress = adapter,
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { car, });
        Mock.Mock<IHttpClientFactory>().Setup(f => f.CreateClient(StaticConstants.HttpClientNameBle))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return handler;
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body);

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
            return respond(request);
        }
    }

    private static DtoBlePresenceResult CreatePresence(bool heard) => new()
    {
        ScannerRunning = true,
        Vehicles = new List<DtoBlePresenceVehicle>
        {
            new() { Vin = TestVin, Heard = heard, },
        },
    };
}
