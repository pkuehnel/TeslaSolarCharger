using System;
using System.Linq;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericModbus;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericRest;
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
            //Normal and hold need to be supported by every controllable device, an empty charge list marks
            //forced charging as not supported.
            Assert.NotEmpty(definition.BatteryControl.NormalWrites);
            Assert.NotEmpty(definition.BatteryControl.HoldWrites);
            var allWrites = definition.BatteryControl.NormalWrites
                .Concat(definition.BatteryControl.HoldWrites)
                .Concat(definition.BatteryControl.ChargeWrites);
            foreach (var write in allWrites)
            {
                if (write.WriteFunction == ModbusWriteFunction.WriteSingleRegister)
                {
                    Assert.True(write.ValueType is ModbusValueType.UShort or ModbusValueType.Short,
                        $"{gatherType} single register write to {write.Address} uses {write.ValueType}");
                }
            }
        }
    }

    [Fact]
    public void UnsupportedChargeModeThrowsNotSupported()
    {
        var definition = ModbusTemplateDefinitions.Definitions[TemplateValueGatherType.VartaModbus];
        Assert.Throws<NotSupportedException>(() => definition.BatteryControl!.GetWrites(HomeBatteryMode.Charge));
    }

    [Fact]
    public void EveryRestDefinitionHasClientSideDefaults()
    {
        foreach (var gatherType in JsonRestTemplateDefinitions.Definitions.Keys)
        {
            Assert.True(GenericRestTemplateSettings.IsGenericRestType(gatherType),
                $"Missing client side defaults for {gatherType}");
        }
        foreach (var gatherType in GenericRestTemplateSettings.GenericRestTypes)
        {
            Assert.True(JsonRestTemplateDefinitions.Definitions.ContainsKey(gatherType),
                $"Missing value map for {gatherType}");
        }
    }

    [Fact]
    public void RestBatteryControlAvailabilityMatchesClientSideDefaults()
    {
        foreach (var (gatherType, definition) in JsonRestTemplateDefinitions.Definitions)
        {
            var defaults = GenericRestTemplateSettings.GetDefaults(gatherType);
            Assert.Equal(defaults.SupportsHomeBatteryControl, definition.BatteryControl != default);
            if (definition.BatteryControl != default)
            {
                Assert.NotEmpty(definition.BatteryControl.NormalRequests);
                Assert.NotEmpty(definition.BatteryControl.HoldRequests);
            }
        }
    }

    [Fact]
    public void RestUriTemplatesResolvePlaceholders()
    {
        var config = new TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic.DtoGenericRestTemplateValueConfiguration
        {
            Host = "192.168.1.5",
            Port = 8080,
            DeviceId = 2,
            MaxBatteryChargePowerW = 3000,
        };
        var resolved = TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericRest
            .GenericJsonRestTemplateValueSetupService.ResolveUriTemplate("http://{host}:{port}/api/{deviceId}?p={maxChargePowerW}", config);
        Assert.Equal("http://192.168.1.5:8080/api/2?p=3000", resolved);
    }

    [Fact]
    public void NoRestUriTemplateContainsUnknownPlaceholders()
    {
        var config = new TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic.DtoGenericRestTemplateValueConfiguration
        {
            Host = "h",
        };
        foreach (var (gatherType, definition) in JsonRestTemplateDefinitions.Definitions)
        {
            var templates = definition.ValueReads.Select(r => r.UriTemplate);
            if (definition.BatteryControl != default)
            {
                templates = templates
                    .Concat(definition.BatteryControl.NormalRequests.Select(r => r.UriTemplate))
                    .Concat(definition.BatteryControl.HoldRequests.Select(r => r.UriTemplate))
                    .Concat(definition.BatteryControl.ChargeRequests.Select(r => r.UriTemplate))
                    .Concat(definition.BatteryControl.NormalRequests.Select(r => r.JsonBodyTemplate ?? string.Empty))
                    .Concat(definition.BatteryControl.HoldRequests.Select(r => r.JsonBodyTemplate ?? string.Empty))
                    .Concat(definition.BatteryControl.ChargeRequests.Select(r => r.JsonBodyTemplate ?? string.Empty));
            }
            var knownPlaceholders = new[] { "{host}", "{port}", "{deviceId}", "{maxChargePowerW}" };
            foreach (var template in templates)
            {
                var resolved = TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericRest
                    .GenericJsonRestTemplateValueSetupService.ResolveUriTemplate(template, config);
                foreach (var placeholder in knownPlaceholders)
                {
                    Assert.False(resolved.Contains(placeholder),
                        $"{gatherType} template '{template}' contains unresolved placeholder {placeholder}");
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
