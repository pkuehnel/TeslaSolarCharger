using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Resources;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BackendApiService : TestBase
{
    private const string BackendApiBaseUrl = "https://api.solar4car.com/api/";
    private const string AccessToken = "backendAccessToken";
    private const string Vin = "LRW3E7FS2NC000001";

    public BackendApiService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void CanEnCodeCorrectUrl()
    {
        var backendApiService = Mock.Create<TeslaSolarCharger.Server.Services.BackendApiService>();
        var configMock = Mock.Mock<IConfigurationWrapper>();
        configMock.Setup(x => x.BackendApiBaseUrl()).Returns("https://api.solar4car.com/api/");
        var url = backendApiService.GenerateAuthUrl("8774fbe7-f9aa-4e36-8e88-5c8b27137f20");
        var expectedUrl = "https://api.solar4car.com/api/AuthRedeem/Redeem?code=8774fbe7-f9aa-4e36-8e88-5c8b27137f20";
        Assert.Equal(expectedUrl, url);
    }

    [Fact]
    public async Task GetFleetApiCommandBudget_NoBackendToken_ReturnsErrorWithoutRequest()
    {
        var handler = SetupBackend(StaticConstants.HttpClientNameShortTimeout, _ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await Mock.Create<TeslaSolarCharger.Server.Services.BackendApiService>().GetFleetApiCommandBudget(Vin);

        Assert.True(result.HasError);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetFleetApiCommandBudget_AsksBackendForTheCarsBudget()
    {
        await AddBackendToken();
        var handler = SetupBackend(StaticConstants.HttpClientNameShortTimeout, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"graceRemainingSeconds\":120,\"nextCommandInSeconds\":3400,\"nextWakeUpInSeconds\":null,\"nextFleetApiTestInSeconds\":20}"),
        });

        var result = await Mock.Create<TeslaSolarCharger.Server.Services.BackendApiService>().GetFleetApiCommandBudget(Vin);

        Assert.False(result.HasError);
        Assert.Equal(120, result.Data!.GraceRemainingSeconds);
        Assert.Equal(3400, result.Data.NextCommandInSeconds);
        Assert.Null(result.Data.NextWakeUpInSeconds);
        Assert.Equal(20, result.Data.NextFleetApiTestInSeconds);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{BackendApiBaseUrl}FleetApiRequests/CommandBudget?vin={Vin}", request.Uri.ToString());
        Assert.Equal($"Bearer {AccessToken}", request.Authorization);
    }

    [Fact]
    public async Task GetFleetApiCommandBudget_BackendRejects_ReturnsProblemDetails()
    {
        await AddBackendToken();
        SetupBackend(StaticConstants.HttpClientNameShortTimeout, _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"status\":403,\"detail\":\"The car is not part of the Tesla account of the user.\"}"),
        });

        var result = await Mock.Create<TeslaSolarCharger.Server.Services.BackendApiService>().GetFleetApiCommandBudget(Vin);

        Assert.True(result.HasError);
        Assert.Equal("The car is not part of the Tesla account of the user.", result.ErrorMessage);
        Assert.Equal(403, result.ProblemDetails!.Status);
    }

    [Fact]
    public async Task SendRequestToBackend_WithTimeout_UsesClientWithoutShortTimeout()
    {
        var handler = SetupBackend(StaticConstants.HttpClientNameDefaultTimeout, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"statusCode\":200,\"jsonResponse\":\"{}\"}"),
        });

        var result = await Mock.Create<TeslaSolarCharger.Server.Services.BackendApiService>()
            .SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, AccessToken, "FleetApiRequests/TestFleetApiAccess", null, TimeSpan.FromSeconds(60));

        Assert.False(result.HasError);
        Assert.Single(handler.Requests);
        Mock.Mock<IHttpClientFactory>().Verify(f => f.CreateClient(StaticConstants.HttpClientNameShortTimeout), Times.Never);
    }

    [Fact]
    public async Task SendRequestToBackend_TimeoutExceeded_ReturnsError()
    {
        SetupBackend(StaticConstants.HttpClientNameDefaultTimeout, async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await Mock.Create<TeslaSolarCharger.Server.Services.BackendApiService>()
            .SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, AccessToken, "FleetApiRequests/TestFleetApiAccess", null, TimeSpan.FromMilliseconds(50));

        Assert.True(result.HasError);
    }

    private CapturingHttpMessageHandler SetupBackend(string httpClientName, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        SetupBackend(httpClientName, new CapturingHttpMessageHandler(respond));

    private CapturingHttpMessageHandler SetupBackend(string httpClientName, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
        SetupBackend(httpClientName, new CapturingHttpMessageHandler(respond));

    private CapturingHttpMessageHandler SetupBackend(string httpClientName, CapturingHttpMessageHandler handler)
    {
        Mock.Mock<IConfigurationWrapper>().Setup(x => x.BackendApiBaseUrl()).Returns(BackendApiBaseUrl);
        Mock.Mock<IHttpClientFactory>().Setup(f => f.CreateClient(httpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return handler;
    }

    private async Task AddBackendToken()
    {
        Context.BackendTokens.Add(new BackendToken(AccessToken, "backendRefreshToken") { ExpiresAtUtc = CurrentFakeDate.AddHours(1), });
        await Context.SaveChangesAsync();
    }
}
