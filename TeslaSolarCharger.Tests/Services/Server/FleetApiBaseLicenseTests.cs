using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Xunit;
using FleetApiService = TeslaSolarCharger.Server.Services.TeslaFleetApiService;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// Without a base license no request is sent to the car, and that must not show up as a misleading deserialization error.
/// </summary>
[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class FleetApiBaseLicenseTests(ITestOutputHelper outputHelper) : FleetApiFallbackTestBase(outputHelper)
{
    private void SetupBaseAppNotLicensed() =>
        Mock.Mock<IBackendApiService>().Setup(b => b.IsBaseAppLicensed(true)).ReturnsAsync(new Result<bool?>(false, null, null));

    private void VerifyErrorRaised(string issueKey, Times times) =>
        Mock.Mock<IErrorHandlingService>().Verify(
            e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), issueKey, Vin,
                It.IsAny<string?>()),
            times);

    [Fact]
    public async Task Command_BaseAppNotLicensed_IsSentNeitherViaBleNorViaTheTeslaCloud()
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        SetupBaseAppNotLicensed();
        var service = CreateService();

        var result = await service.SendCommandToTeslaApi<DtoVehicleWakeUpResult>(Vin, service.WakeUpRequest);

        Assert.NotNull(result);
        Assert.Equal(FleetApiService.BaseAppNotLicensedError, result.Error);
        Assert.Equal("Solar4Car has no active base license, so no command was sent to the car (neither via BLE nor via the Tesla cloud).",
            result.ErrorDescription);
        Mock.Mock<IBleService>().Verify(b => b.WakeUpCar(It.IsAny<string>()), Times.Never);
        VerifyBackendCalled(Constants.WakeUpRequestUrl, Times.Never());
    }

    [Fact]
    public async Task RefreshVehicleOnlineState_BaseAppNotLicensed_ResolvesTheVehicleErrorInsteadOfRaisingIt()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        SetupBaseAppNotLicensed();

        await CreateService().RefreshVehicleOnlineState(car);

        VerifyErrorRaised(IssueKeys.GetVehicle, Times.Never());
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleErrorResolved(IssueKeys.GetVehicle, Vin), Times.Once);
        VerifyBackendCalled(Constants.VehicleRequestUrl, Times.Never());
    }

    [Fact]
    public async Task RefreshCarData_BaseAppNotLicensed_RaisesNoVehicleDataError()
    {
        SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        SetupBaseAppNotLicensed();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataFromTesla()).Returns(true);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.MaxTravelSpeedMetersPerSecond()).Returns(50);

        await CreateService().RefreshCarData();

        VerifyErrorRaised(IssueKeys.GetVehicle, Times.Never());
        VerifyErrorRaised(IssueKeys.GetVehicleData, Times.Never());
        Mock.Mock<IErrorHandlingService>().Verify(e => e.HandleErrorResolved(IssueKeys.GetVehicleData, Vin), Times.Once);
    }

    [Fact]
    public async Task RefreshVehicleOnlineState_OtherRefusal_ShowsItsReasonInsteadOfADeserializationError()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        Mock.Mock<ITokenHelper>().Setup(t => t.GetBackendTokenState(true)).ReturnsAsync(TokenState.Expired);

        await CreateService().RefreshVehicleOnlineState(car);

        Mock.Mock<IErrorHandlingService>().Verify(
            e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                "Could not get vehicle: The Solar4Car backend token is not up to date. Check the Tesla token state in the settings.",
                IssueKeys.GetVehicle, Vin, It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public void MissingResponseMessage_NoResponse_IsADeserializationError()
    {
        Assert.Equal("Could not deserialize vehicle: null",
            FleetApiService.GetMissingResponseMessage<DtoVehicleResult>("vehicle", null));
    }

    [Fact]
    public void MissingResponseMessage_ResponseWithoutError_IsADeserializationErrorShowingTheResponse()
    {
        var message = FleetApiService.GetMissingResponseMessage("vehicle", new DtoGenericTeslaResponse<DtoVehicleResult>());

        Assert.StartsWith("Could not deserialize vehicle: {", message);
    }

    [Theory]
    [InlineData("vehicle unavailable", null, "Could not get vehicle data: vehicle unavailable")]
    [InlineData("vehicle unavailable", "", "Could not get vehicle data: vehicle unavailable")]
    [InlineData("vehicle unavailable", "The car is offline", "Could not get vehicle data: The car is offline")]
    public void MissingResponseMessage_ResponseWithError_ShowsTheReason(string error, string? errorDescription, string expectedMessage)
    {
        var response = new DtoGenericTeslaResponse<DtoVehicleDataResult> { Error = error, ErrorDescription = errorDescription, };

        Assert.Equal(expectedMessage, FleetApiService.GetMissingResponseMessage("vehicle data", response));
    }
}
