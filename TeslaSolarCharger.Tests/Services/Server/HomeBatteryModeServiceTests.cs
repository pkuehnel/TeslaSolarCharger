using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Kostal;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Sma;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.TeslaPowerwall;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;
using HomeBatteryModeService = TeslaSolarCharger.Server.Services.HomeBatteryControl.HomeBatteryModeService;

namespace TeslaSolarCharger.Tests.Services.Server;

public class HomeBatteryModeServiceTests : TestBase
{
    public HomeBatteryModeServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    //No required mode and mode never modified -> nothing to write
    [InlineData(HomeBatteryMode.Unknown, null, 50, 95, null)]
    [InlineData(HomeBatteryMode.Normal, null, 50, 95, null)]
    //Required mode differs from current mode -> write it
    [InlineData(HomeBatteryMode.Unknown, HomeBatteryMode.Hold, 50, 95, HomeBatteryMode.Hold)]
    [InlineData(HomeBatteryMode.Normal, HomeBatteryMode.Charge, 50, 95, HomeBatteryMode.Charge)]
    [InlineData(HomeBatteryMode.Unknown, HomeBatteryMode.Normal, 50, 95, HomeBatteryMode.Normal)]
    //Required mode equals current mode -> write only on transitions
    [InlineData(HomeBatteryMode.Hold, HomeBatteryMode.Hold, 50, 95, null)]
    [InlineData(HomeBatteryMode.Normal, HomeBatteryMode.Normal, 50, 95, null)]
    //No required mode anymore but mode was modified -> restore normal
    [InlineData(HomeBatteryMode.Hold, null, 50, 95, HomeBatteryMode.Normal)]
    [InlineData(HomeBatteryMode.Charge, null, 50, 95, HomeBatteryMode.Normal)]
    //Charge is demoted to hold when max charge soc is reached
    [InlineData(HomeBatteryMode.Unknown, HomeBatteryMode.Charge, 95, 95, HomeBatteryMode.Hold)]
    [InlineData(HomeBatteryMode.Charge, HomeBatteryMode.Charge, 96, 95, HomeBatteryMode.Hold)]
    [InlineData(HomeBatteryMode.Hold, HomeBatteryMode.Charge, 96, 95, null)]
    //Unknown soc does not demote charge
    [InlineData(HomeBatteryMode.Unknown, HomeBatteryMode.Charge, null, 95, HomeBatteryMode.Charge)]
    public void CalculatesCorrectModeToWrite(HomeBatteryMode currentMode, HomeBatteryMode? requiredMode, int? homeBatterySoc,
        int maxChargeSoc, HomeBatteryMode? expectedModeToWrite)
    {
        var result = HomeBatteryModeService.CalculateModeToWrite(currentMode, requiredMode, homeBatterySoc, maxChargeSoc);
        Assert.Equal(expectedModeToWrite, result);
    }

    [Fact]
    public void OverrideIsActiveBeforeExpiry()
    {
        var result = HomeBatteryModeService.GetActiveOverrideMode(HomeBatteryMode.Hold, CurrentFakeDate.AddMinutes(5), CurrentFakeDate);
        Assert.Equal(HomeBatteryMode.Hold, result);
    }

    [Fact]
    public void OverrideExpires()
    {
        var result = HomeBatteryModeService.GetActiveOverrideMode(HomeBatteryMode.Hold, CurrentFakeDate.AddMinutes(-1), CurrentFakeDate);
        Assert.Null(result);
    }

    [Fact]
    public void OverrideWithoutExpiryIsInactive()
    {
        var result = HomeBatteryModeService.GetActiveOverrideMode(HomeBatteryMode.Hold, null, CurrentFakeDate);
        Assert.Null(result);
    }

    [Fact]
    public void NoOverrideModeIsInactive()
    {
        var result = HomeBatteryModeService.GetActiveOverrideMode(null, CurrentFakeDate.AddMinutes(5), CurrentFakeDate);
        Assert.Null(result);
    }

    [Fact]
    public void AutomaticMode_NoWindows_ReturnsNull()
    {
        var result = HomeBatteryModeService.CalculateAutomaticMode(new List<DtoHomeBatteryScheduleWindow>(), CurrentFakeDate, 50, false);
        Assert.Null(result);
    }

    [Fact]
    public void AutomaticMode_InactiveWindows_ReturnNull()
    {
        var windows = new List<DtoHomeBatteryScheduleWindow>
        {
            CreateWindow(HomeBatteryMode.Hold, CurrentFakeDate.AddHours(1), CurrentFakeDate.AddHours(2)),
            CreateWindow(HomeBatteryMode.Charge, CurrentFakeDate.AddHours(-2), CurrentFakeDate.AddHours(-1)),
        };
        var result = HomeBatteryModeService.CalculateAutomaticMode(windows, CurrentFakeDate, 50, false);
        Assert.Null(result);
    }

    [Theory]
    //Soc at or below the guard -> window applies
    [InlineData(30, 40, HomeBatteryMode.Hold)]
    [InlineData(40, 40, HomeBatteryMode.Hold)]
    //Soc above the guard -> energy is not needed, window is skipped
    [InlineData(41, 40, null)]
    //Unknown soc -> guarded window is skipped to be safe
    [InlineData(null, 40, null)]
    public void AutomaticMode_HoldWindowSocGuard(int? homeBatterySoc, int guardSoc, HomeBatteryMode? expectedMode)
    {
        var window = CreateWindow(HomeBatteryMode.Hold, CurrentFakeDate.AddMinutes(-5), CurrentFakeDate.AddMinutes(5));
        window.OnlyWhileSocAtOrBelowPercent = guardSoc;
        var result = HomeBatteryModeService.CalculateAutomaticMode(new List<DtoHomeBatteryScheduleWindow> { window, },
            CurrentFakeDate, homeBatterySoc, false);
        Assert.Equal(expectedMode, result);
    }

    [Theory]
    //Below the target soc -> keep charging
    [InlineData(30, HomeBatteryMode.Charge)]
    //Target soc reached -> demote to hold to keep the bought energy without buying more
    [InlineData(40, HomeBatteryMode.Hold)]
    [InlineData(50, HomeBatteryMode.Hold)]
    //Unknown soc -> keep charging, the max charge soc guard in CalculateModeToWrite already warns
    [InlineData(null, HomeBatteryMode.Charge)]
    public void AutomaticMode_ChargeWindowTargetSoc(int? homeBatterySoc, HomeBatteryMode expectedMode)
    {
        var window = CreateWindow(HomeBatteryMode.Charge, CurrentFakeDate.AddMinutes(-5), CurrentFakeDate.AddMinutes(5));
        window.TargetSocPercent = 40;
        var result = HomeBatteryModeService.CalculateAutomaticMode(new List<DtoHomeBatteryScheduleWindow> { window, },
            CurrentFakeDate, homeBatterySoc, false);
        Assert.Equal(expectedMode, result);
    }

    [Fact]
    public void AutomaticMode_ChargeWinsOverOverlappingHold()
    {
        var windows = new List<DtoHomeBatteryScheduleWindow>
        {
            CreateWindow(HomeBatteryMode.Hold, CurrentFakeDate.AddMinutes(-5), CurrentFakeDate.AddMinutes(5)),
            CreateWindow(HomeBatteryMode.Charge, CurrentFakeDate.AddMinutes(-5), CurrentFakeDate.AddMinutes(5)),
        };
        var result = HomeBatteryModeService.CalculateAutomaticMode(windows, CurrentFakeDate, 20, false);
        Assert.Equal(HomeBatteryMode.Charge, result);
    }

    [Fact]
    public void AutomaticMode_ActiveHomeBatteryDischarging_SuppressesWindows()
    {
        var windows = new List<DtoHomeBatteryScheduleWindow>
        {
            CreateWindow(HomeBatteryMode.Hold, CurrentFakeDate.AddMinutes(-5), CurrentFakeDate.AddMinutes(5)),
            CreateWindow(HomeBatteryMode.Charge, CurrentFakeDate.AddMinutes(-5), CurrentFakeDate.AddMinutes(5)),
        };
        var result = HomeBatteryModeService.CalculateAutomaticMode(windows, CurrentFakeDate, 20, true);
        Assert.Null(result);
    }

    //Test plan case 14: full service wiring of ApplyRequiredModeAsync: an active planned hold window is written to the
    //controllers when grid price based control is enabled.
    [Fact]
    public async Task ApplyRequiredMode_ActiveHoldWindow_WritesHoldToControllers()
    {
        var writtenModes = SetupSingleControllerCapturingWrites();
        SetupAutomaticControl(gridPriceBasedControlEnabled: true);
        var service = Mock.Create<HomeBatteryModeService>();

        await service.ApplyRequiredModeAsync(CancellationToken.None);

        Assert.Equal(new[] { HomeBatteryMode.Hold, }, writtenModes);
    }

    //Test plan case 14: a manual override wins over the automatic mode from planned schedule windows, and an unchanged
    //mode is not rewritten on the next apply.
    [Fact]
    public async Task ApplyRequiredMode_ManualOverride_WinsOverScheduleWindows()
    {
        var writtenModes = SetupSingleControllerCapturingWrites();
        SetupAutomaticControl(gridPriceBasedControlEnabled: true);
        var service = Mock.Create<HomeBatteryModeService>();

        //The windows require hold, but the user manually forces charge.
        await service.SetManualModeAsync(HomeBatteryMode.Charge, TimeSpan.FromMinutes(30), CancellationToken.None);
        await service.ApplyRequiredModeAsync(CancellationToken.None);

        Assert.Equal(new[] { HomeBatteryMode.Charge, }, writtenModes);
    }

    //Test plan case 14: with grid price based control disabled, leftover schedule windows from a previous toggle-on
    //period are ignored and no mode is written.
    [Fact]
    public async Task ApplyRequiredMode_AutomaticControlDisabled_IgnoresLeftoverScheduleWindows()
    {
        var writtenModes = SetupSingleControllerCapturingWrites();
        SetupAutomaticControl(gridPriceBasedControlEnabled: false);
        var service = Mock.Create<HomeBatteryModeService>();

        await service.ApplyRequiredModeAsync(CancellationToken.None);

        Assert.Empty(writtenModes);
    }

    /// <summary>
    /// Sets up settings with an active hold window and the configuration for automatic home battery control.
    /// </summary>
    private void SetupAutomaticControl(bool gridPriceBasedControlEnabled)
    {
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.GridPriceBasedHomeBatteryControl()).Returns(gridPriceBasedControlEnabled);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryMaxChargeSoc()).Returns(95);
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.HomeBatterySoc = 50;
        settingsMock.Object.IsHomeBatteryDischargingActive = false;
        settingsMock.Object.HomeBatteryScheduleWindows = new ConcurrentBag<DtoHomeBatteryScheduleWindow>
        {
            CreateWindow(HomeBatteryMode.Hold, CurrentFakeDate.AddMinutes(-5), CurrentFakeDate.AddMinutes(5)),
        };
    }

    /// <summary>
    /// Provides a single home battery mode controller through the scoped setup services and captures all mode writes.
    /// </summary>
    private List<HomeBatteryMode> SetupSingleControllerCapturingWrites()
    {
        var writtenModes = new List<HomeBatteryMode>();
        var controller = new DtoHomeBatteryModeController(1, "TestController", (mode, _) =>
        {
            writtenModes.Add(mode);
            return Task.CompletedTask;
        }, null);
        var setupServiceMock = new Mock<IHomeBatteryModeSetupService>();
        setupServiceMock.Setup(s => s.GetControllersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DtoHomeBatteryModeController> { controller, });
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(p => p.GetService(typeof(IEnumerable<IHomeBatteryModeSetupService>)))
            .Returns(new[] { setupServiceMock.Object, });
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        Mock.Mock<IServiceScopeFactory>().Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        return writtenModes;
    }

    private static DtoHomeBatteryScheduleWindow CreateWindow(HomeBatteryMode mode, DateTimeOffset validFrom, DateTimeOffset validTo)
    {
        return new DtoHomeBatteryScheduleWindow
        {
            ValidFrom = validFrom,
            ValidTo = validTo,
            Mode = mode,
        };
    }

    [Theory]
    //Normal restores default operating mode and power limits
    [InlineData(HomeBatteryMode.Normal, 2424, 0, 4200, 0, 3300)]
    //Hold keeps default operating mode but sets max discharge power to 0
    [InlineData(HomeBatteryMode.Hold, 2424, 0, 4200, 0, 0)]
    //Charge sets battery charge operating mode with min and max charge power set to max charge power
    [InlineData(HomeBatteryMode.Charge, 2289, 4200, 4200, 0, 0)]
    public void SmaRegisterWritesAreCorrect(HomeBatteryMode mode, int expectedOperatingMode, int expectedMinChargePower,
        int expectedMaxChargePower, int expectedMinDischargePower, int expectedMaxDischargePower)
    {
        var writes = SmaHybridInverterHomeBatteryModeService.GetRegisterWrites(mode, 4200, 3300);
        Assert.Equal(6, writes.Count);
        Assert.Equal(expectedOperatingMode, writes.Single(w => w.Address == 40236).Value);
        Assert.Equal(expectedMinChargePower, writes.Single(w => w.Address == 40793).Value);
        Assert.Equal(expectedMaxChargePower, writes.Single(w => w.Address == 40795).Value);
        Assert.Equal(expectedMinDischargePower, writes.Single(w => w.Address == 40797).Value);
        Assert.Equal(expectedMaxDischargePower, writes.Single(w => w.Address == 40799).Value);
        Assert.Equal(0, writes.Single(w => w.Address == 40801).Value);
    }

    [Fact]
    public void KostalNormalResetsChargeSetpoint()
    {
        var writes = KostalHybridInverterHomeBatteryModeService.GetRegisterWrites(HomeBatteryMode.Normal, 4200);
        var write = Assert.Single(writes);
        Assert.Equal(1034, write.Address);
        Assert.Equal(0, write.Value);
    }

    [Fact]
    public void KostalHoldSetsDischargeLimitToZero()
    {
        var writes = KostalHybridInverterHomeBatteryModeService.GetRegisterWrites(HomeBatteryMode.Hold, 4200);
        var write = Assert.Single(writes);
        Assert.Equal(1040, write.Address);
        Assert.Equal(0, write.Value);
    }

    [Fact]
    public void KostalChargeSetsNegativeChargeSetpoint()
    {
        var writes = KostalHybridInverterHomeBatteryModeService.GetRegisterWrites(HomeBatteryMode.Charge, 4200);
        var write = Assert.Single(writes);
        Assert.Equal(1034, write.Address);
        Assert.Equal(-4200, write.Value);
    }

    [Theory]
    //Normal restores the configured backup reserve
    [InlineData(HomeBatteryMode.Normal, 20, 55, 95, 20)]
    //Hold sets the reserve to the current soc so the battery does not discharge below it
    [InlineData(HomeBatteryMode.Hold, 20, 55, 95, 55)]
    //Hold does not set the reserve below the configured default reserve
    [InlineData(HomeBatteryMode.Hold, 20, 10, 95, 20)]
    //Charge sets the reserve to the max charge soc so the battery charges up to it
    [InlineData(HomeBatteryMode.Charge, 20, 55, 95, 95)]
    public void PowerwallBackupReserveIsCorrect(HomeBatteryMode mode, int normalModeReservePercent, int? currentSoc,
        int maxChargeSoc, int expectedReserve)
    {
        var result = TeslaPowerwallHomeBatteryModeService.GetBackupReservePercent(mode, normalModeReservePercent, currentSoc, maxChargeSoc);
        Assert.Equal(expectedReserve, result);
    }

    [Fact]
    public void PowerwallHoldWithoutSocThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TeslaPowerwallHomeBatteryModeService.GetBackupReservePercent(HomeBatteryMode.Hold, 20, null, 95));
    }

    [Fact]
    public void ModbusWriteBytesAreInMachineOrder()
    {
        Assert.Equal(BitConverter.GetBytes(2424u), ModbusValueExecutionService.GetMachineOrderBytes(ModbusValueType.UInt, 2424));
        Assert.Equal(BitConverter.GetBytes(-3000f), ModbusValueExecutionService.GetMachineOrderBytes(ModbusValueType.Float, -3000));
        Assert.Equal(BitConverter.GetBytes((short)-42), ModbusValueExecutionService.GetMachineOrderBytes(ModbusValueType.Short, -42));
        Assert.Equal(BitConverter.GetBytes((ushort)42), ModbusValueExecutionService.GetMachineOrderBytes(ModbusValueType.UShort, 42));
        Assert.Equal(BitConverter.GetBytes(-100000), ModbusValueExecutionService.GetMachineOrderBytes(ModbusValueType.Int, -100000));
    }

    [Fact]
    public void ModbusWriteBytesThrowOnOverflow()
    {
        Assert.Throws<OverflowException>(() => ModbusValueExecutionService.GetMachineOrderBytes(ModbusValueType.UShort, -1));
        Assert.Throws<OverflowException>(() => ModbusValueExecutionService.GetMachineOrderBytes(ModbusValueType.Short, 70000));
    }

    [Fact]
    public void ModbusWriteBytesThrowOnUnsupportedValueType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusValueExecutionService.GetMachineOrderBytes(ModbusValueType.Bool, 1));
    }
}
