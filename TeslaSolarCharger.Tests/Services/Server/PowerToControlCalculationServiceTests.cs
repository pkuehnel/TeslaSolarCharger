using System.Collections.Generic;
using Moq;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Localization;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// <see cref="TeslaSolarCharger.Server.Services.PowerToControlCalculationService.GetBatteryTargetChargingPower()"/> (public,
/// no reason side effect) and the home battery reservation inside <c>CalculatePowerToControl</c> (private, records a
/// not-charging reason) used to be two separately implemented copies of the same "reserve HomeBatteryChargingPower
/// while SoC is below MinSoc" rule. They were merged into one private core. These tests pin the behavior of both call
/// sites to make sure the merge did not change outcomes.
/// </summary>
public class PowerToControlCalculationServiceTests : TestBase
{
    public PowerToControlCalculationServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private void SetupConfig(int? homeBatteryMinSoc, int? homeBatteryChargingPower)
    {
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(homeBatteryMinSoc);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(homeBatteryChargingPower);
    }

    #region Public no-arg overload (GetBatteryTargetChargingPower)

    [Fact]
    public void GetBatteryTargetChargingPower_ReturnsChargingPower_WhenSocBelowMinSoc()
    {
        SetupConfig(homeBatteryMinSoc: 50, homeBatteryChargingPower: 3_000);
        Mock.Mock<ISettings>().Setup(s => s.HomeBatterySoc).Returns(40);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        var result = service.GetBatteryTargetChargingPower();

        Assert.Equal(3_000, result);
    }

    [Fact]
    public void GetBatteryTargetChargingPower_ReturnsZero_WhenSocAtOrAboveMinSoc()
    {
        SetupConfig(homeBatteryMinSoc: 50, homeBatteryChargingPower: 3_000);
        Mock.Mock<ISettings>().Setup(s => s.HomeBatterySoc).Returns(50);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        var result = service.GetBatteryTargetChargingPower();

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBatteryTargetChargingPower_ReturnsZero_WhenMinSocUnknown()
    {
        SetupConfig(homeBatteryMinSoc: null, homeBatteryChargingPower: 3_000);
        Mock.Mock<ISettings>().Setup(s => s.HomeBatterySoc).Returns(10);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        var result = service.GetBatteryTargetChargingPower();

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBatteryTargetChargingPower_ReturnsZero_WhenChargingPowerUnknown()
    {
        SetupConfig(homeBatteryMinSoc: 50, homeBatteryChargingPower: null);
        Mock.Mock<ISettings>().Setup(s => s.HomeBatterySoc).Returns(10);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        var result = service.GetBatteryTargetChargingPower();

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBatteryTargetChargingPower_DoesNotRecordNotChargingReason()
    {
        SetupConfig(homeBatteryMinSoc: 50, homeBatteryChargingPower: 3_000);
        Mock.Mock<ISettings>().Setup(s => s.HomeBatterySoc).Returns(40);
        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        service.GetBatteryTargetChargingPower();

        reasonHelperMock.Verify(h => h.AddGenericReason(It.IsAny<NotChargingWithExpectedPowerReasonTemplate>()), Times.Never);
    }

    #endregion

    #region Private overload, reached through CalculatePowerToControl (records a not-charging reason)

    /// <summary>
    /// Minimal setup to reach the home battery reservation branch of <c>CalculatePowerToControl</c> without exercising
    /// the (unrelated) too-late-changes / power-buffer / inverter-power paths: an empty loadpoint list skips
    /// <c>HasTooLateChanges</c>, <c>Overage</c> being set makes grid power available so the inverter-power branch is
    /// skipped, and <c>PowerBuffer</c> is 0 so no buffer reason is recorded.
    /// </summary>
    private void SetupCalculatePowerToControlScenario(int? homeBatteryMinSoc, int? homeBatteryChargingPower,
        int? homeBatterySoc, int homeBatteryPower, int overage)
    {
        SetupConfig(homeBatteryMinSoc, homeBatteryChargingPower);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.PowerBuffer()).Returns(0);
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.Overage).Returns(overage);
        settingsMock.Setup(s => s.HomeBatterySoc).Returns(homeBatterySoc);
        settingsMock.Setup(s => s.HomeBatteryPower).Returns(homeBatteryPower);
    }

    [Fact]
    public void CalculatePowerToControl_ReservesChargingPowerAndRecordsReason_WhenSocBelowMinSoc()
    {
        // Battery below min SoC => batteryMinChargingPower = 3000, overage += (HomeBatteryPower - 3000) = 1000 - 3000 = -2000
        SetupCalculatePowerToControlScenario(homeBatteryMinSoc: 50, homeBatteryChargingPower: 3_000,
            homeBatterySoc: 40, homeBatteryPower: 1_000, overage: 5_000);
        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        var result = service.CalculatePowerToControl(new List<DtoLoadPointWithCurrentChargingValues>());

        Assert.Equal(3_000, result);
        reasonHelperMock.Verify(h => h.AddGenericReason(It.Is<NotChargingWithExpectedPowerReasonTemplate>(
            r => r.LocalizationKey == TranslationKeys.NotChargingReasonReservedForHomeBattery
                 && r.FormatArguments![0].Equals(3_000)
                 && r.FormatArguments![1].Equals(40)
                 && r.FormatArguments![2].Equals(50))),
            Times.Once);
    }

    [Fact]
    public void CalculatePowerToControl_DoesNotReserveOrRecordReason_WhenSocAtOrAboveMinSoc()
    {
        // Battery at/above min SoC => batteryMinChargingPower = 0, overage += (HomeBatteryPower - 0) = 1000
        SetupCalculatePowerToControlScenario(homeBatteryMinSoc: 50, homeBatteryChargingPower: 3_000,
            homeBatterySoc: 50, homeBatteryPower: 1_000, overage: 5_000);
        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        var result = service.CalculatePowerToControl(new List<DtoLoadPointWithCurrentChargingValues>());

        Assert.Equal(6_000, result);
        reasonHelperMock.Verify(h => h.AddGenericReason(It.Is<NotChargingWithExpectedPowerReasonTemplate>(
            r => r.LocalizationKey == TranslationKeys.NotChargingReasonReservedForHomeBattery)),
            Times.Never);
    }

    [Fact]
    public void CalculatePowerToControl_IgnoresHomeBattery_WhenMinSocUnknown()
    {
        // homeBatteryMinSoc unknown => AddHomeBatteryStateToPowerCalculation returns overage unchanged, before even
        // reaching the (merged) reservation logic.
        SetupCalculatePowerToControlScenario(homeBatteryMinSoc: null, homeBatteryChargingPower: 3_000,
            homeBatterySoc: 10, homeBatteryPower: 1_000, overage: 5_000);
        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        var result = service.CalculatePowerToControl(new List<DtoLoadPointWithCurrentChargingValues>());

        Assert.Equal(5_000, result);
        reasonHelperMock.Verify(h => h.AddGenericReason(It.Is<NotChargingWithExpectedPowerReasonTemplate>(
            r => r.LocalizationKey == TranslationKeys.NotChargingReasonReservedForHomeBattery)),
            Times.Never);
    }

    [Fact]
    public void CalculatePowerToControl_IgnoresHomeBattery_WhenChargingPowerUnknown()
    {
        SetupCalculatePowerToControlScenario(homeBatteryMinSoc: 50, homeBatteryChargingPower: null,
            homeBatterySoc: 10, homeBatteryPower: 1_000, overage: 5_000);
        var reasonHelperMock = Mock.Mock<INotChargingWithExpectedPowerReasonHelper>();
        var service = Mock.Create<TeslaSolarCharger.Server.Services.PowerToControlCalculationService>();

        var result = service.CalculatePowerToControl(new List<DtoLoadPointWithCurrentChargingValues>());

        Assert.Equal(5_000, result);
        reasonHelperMock.Verify(h => h.AddGenericReason(It.Is<NotChargingWithExpectedPowerReasonTemplate>(
            r => r.LocalizationKey == TranslationKeys.NotChargingReasonReservedForHomeBattery)),
            Times.Never);
    }

    #endregion
}
