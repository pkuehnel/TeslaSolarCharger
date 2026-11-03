using Moq;
using PkSoftwareService.Custom.Backend.Ble;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// A car that waits for power the charger does not supply refuses every charge start with "requested" or "no_power".
/// TSC used to send the start again every 90 s and raised an issue each time.
/// </summary>
[SuppressMessage("ReSharper", "UseConfigureAwaitFalse")]
public class ChargeStartRefusedForMissingPowerTests(ITestOutputHelper outputHelper) : FleetApiFallbackTestBase(outputHelper)
{
    private const string OnlineVehicleJson = "{\"response\":{\"state\":\"online\"}}";

    private static string RefusedCommandJson(string reason) => "{\"response\":{\"result\":false,\"reason\":\"" + reason + "\"}}";

    private DtoCar SetupCarReadyToStart()
    {
        var car = SetupBleCarWithFailingBle(isFleetApiLicensed: true);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GetVehicleDataFromTesla()).Returns(true);
        SetupBackendResponse(Constants.VehicleRequestUrl, TeslaResponse(OnlineVehicleJson));
        SetupBackendResponse(Constants.SetChargingAmpsRequestUrl, Successful());
        return car;
    }

    private void VerifyCommandFailedIssueRaised(Times times)
    {
        Mock.Mock<IErrorHandlingService>().Verify(
            e => e.HandleError(It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(h => h.Contains("could not handle command")),
                It.IsAny<string>(), It.IsAny<string>(), Vin, It.IsAny<string?>()),
            times);
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("no_power")]
    [InlineData("car could not execute command: requested")]
    public async Task FleetApiRefusal_IsRememberedWithoutIssue(string reason)
    {
        var car = SetupCarReadyToStart();
        SetupBackendResponse(Constants.ChargeStartRequestUrl, TeslaResponse(RefusedCommandJson(reason)));

        await CreateService().StartCharging(car.Id, 10);

        Assert.Equal(Clock.DateTimeOffSetUtcNow(), car.ChargeStartRefusedForMissingPower);
        VerifyCommandFailedIssueRaised(Times.Never());
    }

    [Fact]
    public async Task FleetApiRefusalForOtherReason_IsNotRememberedAndRaisesIssue()
    {
        var car = SetupCarReadyToStart();
        SetupBackendResponse(Constants.ChargeStartRequestUrl, TeslaResponse(FailedCommandJson));

        await CreateService().StartCharging(car.Id, 10);

        Assert.Null(car.ChargeStartRefusedForMissingPower);
        VerifyCommandFailedIssueRaised(Times.Once());
    }

    [Fact]
    public async Task SuccessfulStart_IsNotRemembered()
    {
        var car = SetupCarReadyToStart();
        SetupBackendResponse(Constants.ChargeStartRequestUrl, Successful());

        await CreateService().StartCharging(car.Id, 10);

        Assert.Null(car.ChargeStartRefusedForMissingPower);
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("no_power")]
    public async Task BleRefusal_IsRememberedWithoutFleetApiFallback(string carErrorMessage)
    {
        var car = SetupCarReadyToStart();
        Mock.Mock<IBleService>().Setup(b => b.StartCharging(Vin)).ReturnsAsync(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = carErrorMessage,
        });

        await CreateService().StartCharging(car.Id, 10);

        Assert.Equal(Clock.DateTimeOffSetUtcNow(), car.ChargeStartRefusedForMissingPower);
        VerifyBackendCalled(Constants.ChargeStartRequestUrl, Times.Never());
    }

    [Fact]
    public async Task BleRefusalBecauseAlreadyCharging_IsNotRemembered()
    {
        var car = SetupCarReadyToStart();
        Mock.Mock<IBleService>().Setup(b => b.StartCharging(Vin)).ReturnsAsync(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = "is_charging",
        });

        await CreateService().StartCharging(car.Id, 10);

        Assert.Null(car.ChargeStartRefusedForMissingPower);
    }

    [Fact]
    public async Task WaitingForChargerPower_SendsNothing()
    {
        var car = SetupCarReadyToStart();
        car.ChargeStartCalls.Add(Clock.UtcNow().AddMinutes(-2));
        car.ChargeStartRefusedForMissingPower = Clock.DateTimeOffSetUtcNow().AddMinutes(-2);
        SetupBackendResponse(Constants.ChargeStartRequestUrl, Successful());

        await CreateService().StartCharging(car.Id, 10);

        Mock.Mock<IBleService>().Verify(b => b.StartCharging(It.IsAny<string>()), Times.Never);
        Mock.Mock<IBleService>().Verify(b => b.SetAmp(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        VerifyBackendCalled(Constants.VehicleRequestUrl, Times.Never());
        VerifyBackendCalled(Constants.SetChargingAmpsRequestUrl, Times.Never());
        VerifyBackendCalled(Constants.ChargeStartRequestUrl, Times.Never());
    }

    [Fact]
    public async Task PilotCurrentChangedAfterRefusal_StartsAgain()
    {
        var car = SetupCarReadyToStart();
        car.ChargeStartCalls.Add(Clock.UtcNow().AddMinutes(-2));
        car.ChargeStartRefusedForMissingPower = Clock.DateTimeOffSetUtcNow().AddMinutes(-2);
        car.ChargerPilotCurrent.Update(Clock.DateTimeOffSetUtcNow().AddMinutes(-1), 16);
        SetupBackendResponse(Constants.ChargeStartRequestUrl, Successful());

        await CreateService().StartCharging(car.Id, 10);

        VerifyBackendCalled(Constants.ChargeStartRequestUrl, Times.Once());
    }
}
