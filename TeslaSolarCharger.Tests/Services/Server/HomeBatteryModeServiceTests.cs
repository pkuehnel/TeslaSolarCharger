using System;
using System.Linq;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Kostal;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Sma;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.TeslaPowerwall;
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
