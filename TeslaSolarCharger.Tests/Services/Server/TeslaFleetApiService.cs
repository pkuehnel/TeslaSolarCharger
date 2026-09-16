using Moq;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;
using Xunit;


namespace TeslaSolarCharger.Tests.Services.Server;

[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class TeslaFleetApiService(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    private const string AccessToken = "backendAccessToken";
    private const string EncryptionKey = "encryption+key";

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
    public async Task GetAllCarsFromAccount_NoBackendToken_ReturnsExpectedError()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var error = FinAssert.Fail(result);
        Assert.False(error.IsExceptional);
        Assert.Equal("No Backend token found.", error.Message);
        VerifyBackendNotCalled();
    }

    [Fact]
    public async Task GetAllCarsFromAccount_NoDecryptionKey_ReturnsExpectedError()
    {
        await AddBackendToken();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var error = FinAssert.Fail(result);
        Assert.False(error.IsExceptional);
        Assert.Equal("No Decryption key found.", error.Message);
        VerifyBackendNotCalled();
    }

    [Fact]
    public async Task GetAllCarsFromAccount_BackendReturnsError_ReturnsHttpRequestException()
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(null, "Backend down", null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var exception = Assert.IsType<HttpRequestException>(FinAssert.Exceptional(result), exactMatch: false);
        Assert.Contains("Backend down", exception.Message);
        Assert.Null(exception.StatusCode);
    }

    [Fact]
    public async Task GetAllCarsFromAccount_BackendReturnsNoData_ReturnsExpectedError()
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(null, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var error = FinAssert.Fail(result);
        Assert.False(error.IsExceptional);
        Assert.Equal("Could not deserialize response body", error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.MultipleChoices)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData((HttpStatusCode)199)]
    public async Task GetAllCarsFromAccount_TeslaReturnsNonSuccessStatusCode_ReturnsHttpRequestExceptionWithStatusCode(HttpStatusCode statusCode)
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(new() { StatusCode = statusCode, JsonResponse = "{\"error\":\"denied\"}", }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var exception = Assert.IsType<HttpRequestException>(FinAssert.Exceptional(result), exactMatch: false);
        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Contains("{\"error\":\"denied\"}", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAllCarsFromAccount_TeslaReturnsEmptyBody_ReturnsExpectedError(string? jsonResponse)
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = jsonResponse, }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var error = FinAssert.Fail(result);
        Assert.False(error.IsExceptional);
        Assert.Equal("Empty Tesla JSON response body from Solar4Car Backend", error.Message);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"response\":null}")]
    public async Task GetAllCarsFromAccount_TeslaReturnsNoVehicleList_ReturnsExpectedError(string jsonResponse)
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = jsonResponse, }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var error = FinAssert.Fail(result);
        Assert.False(error.IsExceptional);
        Assert.Equal("Could not deserialize response body", error.Message);
    }

    [Fact]
    public async Task GetAllCarsFromAccount_TeslaReturnsInvalidJson_ReturnsJsonException()
    {
        await AddBackendTokenAndEncryptionKey();
        SetupBackendResponse(new(new() { StatusCode = HttpStatusCode.OK, JsonResponse = "{not json", }, null, null));
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        Assert.IsType<JsonException>(FinAssert.Exceptional(result), exactMatch: false);
    }

    [Fact]
    public async Task GetAllCarsFromAccount_BackendThrows_ReturnsException()
    {
        await AddBackendTokenAndEncryptionKey();
        var thrownException = new HttpRequestException("Connection refused");
        Mock.Mock<IBackendApiService>()
            .Setup(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(It.IsAny<HttpMethod>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object?>()))
            .ThrowsAsync(thrownException);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TeslaFleetApiService>();

        var result = await service.GetAllCarsFromAccount();

        var exception = Assert.IsType<HttpRequestException>(FinAssert.Exceptional(result), exactMatch: false);
        Assert.Same(thrownException, exception);
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

        var cars = FinAssert.Succ(result);
        Assert.Collection(cars,
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

        Assert.Empty(FinAssert.Succ(result));
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
        var expectedRequestUri = $"FleetApiRequests/GetAllCarsFromAccount?encryptionKey={Uri.EscapeDataString(EncryptionKey)}";
        Mock.Mock<IBackendApiService>()
            .Setup(b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Get, AccessToken, expectedRequestUri, null))
            .ReturnsAsync(response);
    }

    private void VerifyBackendNotCalled()
    {
        Mock.Mock<IBackendApiService>().Verify(
            b => b.SendRequestToBackend<DtoBackendApiTeslaResponse>(It.IsAny<HttpMethod>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object?>()),
            Times.Never);
    }
}
