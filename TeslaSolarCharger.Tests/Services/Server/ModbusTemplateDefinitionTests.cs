using System;
using System.Linq;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericModbus;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ModbusTemplateDefinitionTests : TestBase
{
    public ModbusTemplateDefinitionTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void EveryDefinitionHasClientSideDefaults()
    {
        foreach (var gatherType in ModbusTemplateDefinitions.Definitions.Keys)
        {
            Assert.True(GenericModbusTemplateSettings.IsGenericModbusType(gatherType),
                $"Missing client side defaults for {gatherType}");
        }
        foreach (var gatherType in GenericModbusTemplateSettings.GenericModbusTypes)
        {
            Assert.True(ModbusTemplateDefinitions.Definitions.ContainsKey(gatherType),
                $"Missing register map for {gatherType}");
        }
    }

    [Fact]
    public void BatteryControlAvailabilityMatchesClientSideDefaults()
    {
        foreach (var (gatherType, definition) in ModbusTemplateDefinitions.Definitions)
        {
            var defaults = GenericModbusTemplateSettings.GetDefaults(gatherType);
            Assert.Equal(defaults.SupportsHomeBatteryControl, definition.BatteryControl != default);
        }
    }

    [Fact]
    public void RegisterLengthsMatchValueTypes()
    {
        foreach (var (gatherType, definition) in ModbusTemplateDefinitions.Definitions)
        {
            foreach (var register in definition.ValueRegisters)
            {
                var expectedLength = register.ValueType switch
                {
                    ModbusValueType.Short or ModbusValueType.UShort => 1,
                    ModbusValueType.Int or ModbusValueType.UInt or ModbusValueType.Float => 2,
                    ModbusValueType.Ulong => 4,
                    _ => throw new ArgumentOutOfRangeException(nameof(register.ValueType)),
                };
                Assert.True(expectedLength == register.Length,
                    $"{gatherType} register {register.Address} has length {register.Length} but value type {register.ValueType}");
            }
        }
    }

    [Fact]
    public void SingleRegisterWritesOnlyUse16BitValues()
    {
        foreach (var (gatherType, definition) in ModbusTemplateDefinitions.Definitions)
        {
            if (definition.BatteryControl == default)
            {
                continue;
            }
            foreach (var mode in new[] { HomeBatteryMode.Normal, HomeBatteryMode.Hold, HomeBatteryMode.Charge })
            {
                var writes = definition.BatteryControl.GetWrites(mode);
                Assert.NotEmpty(writes);
                foreach (var write in writes)
                {
                    if (write.WriteFunction == ModbusWriteFunction.WriteSingleRegister)
                    {
                        Assert.True(write.ValueType is ModbusValueType.UShort or ModbusValueType.Short,
                            $"{gatherType} single register write to {write.Address} uses {write.ValueType}");
                    }
                }
            }
        }
    }

    [Fact]
    public void HybridDefinitionsContainSocAndBatteryPower()
    {
        foreach (var (gatherType, definition) in ModbusTemplateDefinitions.Definitions)
        {
            if (definition.BatteryControl == default)
            {
                continue;
            }
            Assert.Contains(definition.ValueRegisters, r => r.UsedFor == ValueUsage.HomeBatterySoc);
            Assert.Contains(definition.ValueRegisters, r => r.UsedFor == ValueUsage.HomeBatteryPower);
        }
    }

    [Theory]
    [InlineData(BatteryModeWriteValueSource.Constant, 42, 1, 42)]
    //Sungrow discharge power register expects 10 W units
    [InlineData(BatteryModeWriteValueSource.MaxDischargePowerW, 0, 0.1, 500)]
    [InlineData(BatteryModeWriteValueSource.MaxChargePowerW, 0, 1, 4000)]
    [InlineData(BatteryModeWriteValueSource.MinSoc, 0, 1, 15)]
    [InlineData(BatteryModeWriteValueSource.MaxChargeSoc, 0, 1, 95)]
    //Current soc 55 is above min soc
    [InlineData(BatteryModeWriteValueSource.CurrentSoc, 0, 1, 55)]
    public void ResolvesWriteValues(BatteryModeWriteValueSource source, decimal constantValue, decimal factor, decimal expectedValue)
    {
        var write = new ModbusBatteryModeWrite
        {
            Address = 1,
            ValueType = ModbusValueType.UShort,
            WriteFunction = ModbusWriteFunction.WriteSingleRegister,
            Source = source,
            ConstantValue = constantValue,
            Factor = factor,
        };
        var result = GenericModbusHomeBatteryModeService.ResolveWriteValue(write, 4000, 5000, 55, 15, 95);
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void CurrentSocIsClampedToMinSoc()
    {
        var write = new ModbusBatteryModeWrite
        {
            Address = 1,
            ValueType = ModbusValueType.UShort,
            WriteFunction = ModbusWriteFunction.WriteSingleRegister,
            Source = BatteryModeWriteValueSource.CurrentSoc,
        };
        var result = GenericModbusHomeBatteryModeService.ResolveWriteValue(write, 4000, 5000, 5, 15, 95);
        Assert.Equal(15, result);
    }

    [Fact]
    public void CurrentSocWithoutKnownSocThrows()
    {
        var write = new ModbusBatteryModeWrite
        {
            Address = 1,
            ValueType = ModbusValueType.UShort,
            WriteFunction = ModbusWriteFunction.WriteSingleRegister,
            Source = BatteryModeWriteValueSource.CurrentSoc,
        };
        Assert.Throws<InvalidOperationException>(() =>
            GenericModbusHomeBatteryModeService.ResolveWriteValue(write, 4000, 5000, null, 15, 95));
    }

    [Fact]
    public void SungrowChargeModeWritesForcedChargeCommand()
    {
        var definition = ModbusTemplateDefinitions.Definitions[TemplateValueGatherType.SungrowHybridInverterModbus];
        var chargeWrites = definition.BatteryControl!.GetWrites(HomeBatteryMode.Charge);
        Assert.Equal(2, chargeWrites.Single(w => w.Address == 13049).ConstantValue);
        Assert.Equal(0xAA, chargeWrites.Single(w => w.Address == 13050).ConstantValue);
        Assert.Equal(BatteryModeWriteValueSource.MaxChargePowerW, chargeWrites.Single(w => w.Address == 13051).Source);
    }

    [Fact]
    public void HuaweiChargeModeRequiresPeriodicRewrite()
    {
        var definition = ModbusTemplateDefinitions.Definitions[TemplateValueGatherType.HuaweiSun2000HybridInverterModbus];
        Assert.Equal(TimeSpan.FromSeconds(30), definition.BatteryControl!.RewriteInterval);
        //47100 = 1 retriggers the forcible charge and needs to be the last write of the sequence
        Assert.Equal(47100, definition.BatteryControl.ChargeWrites.Last().Address);
        Assert.Equal(1, definition.BatteryControl.ChargeWrites.Last().ConstantValue);
    }
}
