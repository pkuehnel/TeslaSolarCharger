using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Car;
using TeslaSolarCharger.Shared.Resources.Contracts;
using Xunit;


namespace TeslaSolarCharger.Tests.Services.Server;

[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class TeslaFleetApiService(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    private const string AccessToken = "backendAccessToken";
    private const string EncryptionKey = "encryption+key";
    private static readonly string ExpectedRequestUri = $"FleetApiRequests/GetAllCarsFromAccount?encryptionKey={Uri.EscapeDataString(EncryptionKey)}";

    [Theory]
    [InlineData(28155, "Retry in 28155 seconds")]
    [InlineData(1, "Retry in 1 seconds")]
    [InlineData(0, "Retry in 0 seconds")]
    [InlineData(5641451, "Retry in 5641451 seconds")]
    public void CanCreateCorrectRetryDateTime(int correctRetryIn, string responseString)
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();
        var seconds = service.RetryInSeconds(responseString);
        Assert.Equal(correctRetryIn, seconds);
    }

    [Fact]
    public async Task GetAllCarsFromAccount_NoBackendToken_ReturnsError()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        AssertErrorWithoutProblemDetails(result, "No Backend token found.");
        VerifyBackendNotCalled();
    }

    [Fact]
    public async Task GetAllCarsFromAccount_NoDecryptionKey_ReturnsError()
    {
        await AddBackendToken();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        AssertErrorWithoutProblemDetails(result, "No Decryption key found.");
        VerifyBackendNotCalled();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAllCarsFromAccount_BackendReturnsError_ReturnsErrorWithBackendProblemDetails(bool backendSentProblemDetails)
    {
        await AddBackendTokenAndEncryptionKey();
        var backendProblemDetails = backendSentProblemDetails ? new ProblemDetails { Detail = "Backend down", Status = 502, } : null;
        SetupBackendResponse(new(null, "Backend down", backendProblemDetails));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        Assert.True(result.HasError);
        Assert.Null(result.Data);
        Assert.Equal($"Requesting {ExpectedRequestUri} returned following error: Backend down", result.ErrorMessage);
        Assert.Same(backendProblemDetails, result.ProblemDetails);
    }

    [Fact]
    public async Task GetAllCarsFromAccount_BackendReturnsNoData_ReturnsError()
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(null, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        AssertErrorWithoutProblemDetails(result, "Could not deserialize response body");
    }

    [Theory]
    [InlineData(HttpStatusCode.MultipleChoices)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData((HttpStatusCode)199)]
    public async Task GetAllCarsFromAccount_TeslaReturnsNonSuccessStatusCode_ReturnsErrorWithTeslaStatusCode(HttpStatusCode statusCode)
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(new() { StatusCode = statusCode, JsonResponse = "{\"error\":\"denied\"}", }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        Assert.True(result.HasError);
        Assert.Null(result.Data);
        Assert.Equal($"Requesting {ExpectedRequestUri} returned following statusCode: {statusCode} Underlaying result: {{\"error\":\"denied\"}}", result.ErrorMessage);
        Assert.NotNull(result.ProblemDetails);
        Assert.Equal((int)statusCode, result.ProblemDetails.Status);
        Assert.Equal(result.ErrorMessage, result.ProblemDetails.Detail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAllCarsFromAccount_TeslaReturnsEmptyBody_ReturnsError(string? jsonResponse)
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = jsonResponse, }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        AssertErrorWithoutProblemDetails(result, "Empty Tesla JSON response body from Solar4Car Backend");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"response\":null}")]
    public async Task GetAllCarsFromAccount_TeslaReturnsNoVehicleList_ReturnsError(string jsonResponse)
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = jsonResponse, }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        AssertErrorWithoutProblemDetails(result, "Could not deserialize response body");
    }

    [Fact]
    public async Task GetAllCarsFromAccount_TeslaReturnsInvalidJson_ReturnsParserMessage()
    {
        await AddBackendTokenAndEncryptionKey();
        const string invalidJson = "{not json";
        SetupBackendResponse(new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = invalidJson, }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var parserException = Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<DtoGenericTeslaResponse<List<DtoVehicleResult>>>(invalidJson));
        AssertErrorWithoutProblemDetails(result, parserException.Message);
    }

    [Fact]
    public async Task GetAllCarsFromAccount_BackendThrows_ReturnsExceptionMessage()
    {
        await AddBackendTokenAndEncryptionKey();
        Mock.Mock<IBackendApiService>()
            .Setup(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(It.IsAny<HttpMethod>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object?>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        AssertErrorWithoutProblemDetails(result, "Connection refused");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData((HttpStatusCode)299)]
    public async Task GetAllCarsFromAccount_TeslaReturnsVehicles_ReturnsMappedCars(HttpStatusCode statusCode)
    {
        await AddBackendTokenAndEncryptionKey();
        const string jsonResponse = "{\"response\":[{\"id\":1,\"vehicle_id\":11,\"vin\":\"VIN1\",\"display_name\":\"Model Y\"},{\"id\":2,\"vehicle_id\":22,\"vin\":\"VIN2\",\"display_name\":null}],\"count\":2}";
        SetupBackendResponse(new(new() { StatusCode = statusCode, JsonResponse = jsonResponse, }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        Assert.False(result.HasError);
        Assert.Null(result.ProblemDetails);
        Assert.NotNull(result.Data);
        Assert.Collection(result.Data,
            car =>
            {
                Assert.Equal("VIN1", car.Vin);
                Assert.Equal("Model Y", car.Name);
            },
            car =>
            {
                Assert.Equal("VIN2", car.Vin);
                Assert.Null(car.Name);
            });
    }

    [Fact]
    public async Task GetAllCarsFromAccount_TeslaReturnsEmptyVehicleList_ReturnsEmptyList()
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = "{\"response\":[],\"count\":0}", }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        Assert.False(result.HasError);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    private static void AssertErrorWithoutProblemDetails(Result<List<DtoTesla>> result, string expectedErrorMessage)
    {
        Assert.True(result.HasError);
        Assert.Equal(expectedErrorMessage, result.ErrorMessage);
        Assert.Null(result.Data);
        Assert.Null(result.ProblemDetails);
    }

    private async Task AddBackendToken()
    {
        Context.BackendTokens.Add(new BackendToken(AccessToken, "backendRefreshToken") { ExpiresAtUtc = CurrentFakeDate.AddHours(1), });
        await Context.SaveChangesAsync();
    }

    private async Task AddBackendTokenAndEncryptionKey()
    {
        await AddBackendToken();
        var encryptionKeyKey = Mock.Create<IConstants>().TeslaTokenEncryptionKeyKey;
        Mock.Mock<ITscConfigurationService>().Setup(c => c.GetConfigurationValueByKey(encryptionKeyKey)).ReturnsAsync(EncryptionKey);
    }

    private void SetupBackendResponse(Result<DtoBackendApiTeslaResponse> response)
    {
        Mock.Mock<IBackendApiService>()
            .Setup(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Get, AccessToken, ExpectedRequestUri, null))
            .ReturnsAsync(response);
    }

    private void VerifyBackendNotCalled()
    {
        Mock.Mock<IBackendApiService>().Verify(
            b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(It.IsAny<HttpMethod>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object?>()),
            Times.Never);
    }
}
