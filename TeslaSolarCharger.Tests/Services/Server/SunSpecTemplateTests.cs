using System.Linq;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class SunSpecTemplateTests : TestBase
{
    public SunSpecTemplateTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData("203:W", 203, null, "W")]
    [InlineData("124:0:StorCtl_Mod", 124, 0, "StorCtl_Mod")]
    [InlineData("160:3:DCW", 160, 3, "DCW")]
    public void ParsesPointReferences(string reference, int expectedModel, int? expectedBlock, string expectedPoint)
    {
        var parsed = SunSpecPointReference.Parse(reference);
        Assert.Equal(expectedModel, parsed.ModelId);
        Assert.Equal(expectedBlock, parsed.Block);
        Assert.Equal(expectedPoint, parsed.Point);
    }

    [Fact]
    public void Model160UsesLongBlockOffsetsForStandardBlock()
    {
        //Standard smdx block is 20 registers with an 8 register IDStr, DCW at block offset 11
        Assert.Equal(11, SunSpecModels.Model160.DcwOffsetInBlock(20));
        Assert.Equal(12, SunSpecModels.Model160.DcwhOffsetInBlock(20));
        //Short block variant without IDStr
        Assert.Equal(4, SunSpecModels.Model160.DcwOffsetInBlock(8));
        Assert.Equal(5, SunSpecModels.Model160.DcwhOffsetInBlock(8));
    }

    [Fact]
    public void SharedInverterModelsHaveExpectedPointOffsets()
    {
        //Integer + scale factor inverter model: W at 12 with scale factor at 13
        var w = SunSpecModels.Models[103].Points["W"];
        Assert.Equal(12, w.Offset);
        Assert.Equal(13, w.ScaleFactorOffset);
        Assert.Equal(SunSpecPointValueType.Int16, w.ValueType);
        //Float inverter model: W at 20, no scale factor
        var floatW = SunSpecModels.Models[113].Points["W"];
        Assert.Equal(20, floatW.Offset);
        Assert.Null(floatW.ScaleFactorOffset);
    }

    [Fact]
    public void Storage124PointsHaveExpectedOffsets()
    {
        var model = SunSpecModels.Models[124];
        Assert.Equal(3, model.Points["StorCtl_Mod"].Offset);
        Assert.Equal(6, model.Points["ChaState"].Offset);
        Assert.Equal(20, model.Points["ChaState"].ScaleFactorOffset);
        Assert.Equal(10, model.Points["OutWRte"].Offset);
        Assert.Equal(23, model.Points["OutWRte"].ScaleFactorOffset);
        Assert.Equal(15, model.Points["ChaGriSet"].Offset);
    }

    [Fact]
    public void Battery802PointsHaveExpectedOffsets()
    {
        var model = SunSpecModels.Models[802];
        Assert.Equal(9, model.Points["SoC"].Offset);
        Assert.Equal(54, model.Points["SoC"].ScaleFactorOffset);
        Assert.Equal(45, model.Points["W"].Offset);
        Assert.Equal(61, model.Points["W"].ScaleFactorOffset);
    }

    [Fact]
    public void EveryDefinitionHasClientSideDefaults()
    {
        foreach (var gatherType in SunSpecTemplateDefinitions.Definitions.Keys)
        {
            Assert.True(SunSpecTemplateSettings.IsSunSpecType(gatherType), $"Missing client side defaults for {gatherType}");
        }
        foreach (var gatherType in SunSpecTemplateSettings.SunSpecTypes)
        {
            Assert.True(SunSpecTemplateDefinitions.Definitions.ContainsKey(gatherType), $"Missing value map for {gatherType}");
        }
    }

    [Fact]
    public void BatteryControlAvailabilityMatchesClientSideDefaults()
    {
        foreach (var (gatherType, definition) in SunSpecTemplateDefinitions.Definitions)
        {
            var defaults = SunSpecTemplateSettings.GetDefaults(gatherType);
            Assert.Equal(defaults.SupportsHomeBatteryControl, definition.BatteryControl != default);
        }
    }

    [Fact]
    public void EveryReferencedPointExistsInModels()
    {
        foreach (var (gatherType, definition) in SunSpecTemplateDefinitions.Definitions)
        {
            foreach (var pointReference in definition.ValueReads.SelectMany(v => v.Components).SelectMany(c => c.PointFallbacks))
            {
                AssertPointExists(gatherType, pointReference);
            }
            if (definition.BatteryControl == default)
            {
                continue;
            }
            var allWrites = definition.BatteryControl.NormalWrites
                .Concat(definition.BatteryControl.HoldWrites)
                .Concat(definition.BatteryControl.ChargeWrites);
            foreach (var write in allWrites.Where(w => w.SunSpecPointReference != default))
            {
                AssertPointExists(gatherType, write.SunSpecPointReference!);
            }
        }
    }

    [Fact]
    public void PlainRegisterWritesHaveAddressAndSunSpecWritesHaveReference()
    {
        foreach (var (_, definition) in SunSpecTemplateDefinitions.Definitions)
        {
            if (definition.BatteryControl == default)
            {
                continue;
            }
            var allWrites = definition.BatteryControl.NormalWrites
                .Concat(definition.BatteryControl.HoldWrites)
                .Concat(definition.BatteryControl.ChargeWrites);
            foreach (var write in allWrites)
            {
                //Exactly one of the two write kinds must be set
                Assert.True((write.SunSpecPointReference != default) ^ (write.PlainRegisterAddress != default));
            }
        }
    }

    [Theory]
    [InlineData(SunSpecWriteValueSource.Constant, 42, 100, 4000, 42)]
    [InlineData(SunSpecWriteValueSource.NegativeMaxChargeRatePercent, 0, 80, 4000, -80)]
    [InlineData(SunSpecWriteValueSource.NegativeMaxChargePowerW, 0, 100, 4000, -4000)]
    public void ResolvesWriteValues(SunSpecWriteValueSource source, decimal constantValue, int maxChargeRatePercent,
        int maxChargePowerW, decimal expected)
    {
        var write = new SunSpecBatteryModeWrite
        {
            SunSpecPointReference = "124:0:OutWRte",
            WriteFunction = ModbusWriteFunction.WriteMultipleRegisters,
            Source = source,
            ConstantValue = constantValue,
        };
        var result = GenericSunSpecHomeBatteryModeService.ResolveWriteValue(write, maxChargeRatePercent, maxChargePowerW);
        Assert.Equal(expected, result);
    }

    private static void AssertPointExists(TemplateValueGatherType gatherType, string pointReference)
    {
        var reference = SunSpecPointReference.Parse(pointReference);
        if (reference.ModelId == SunSpecModels.Model160.ModelId)
        {
            Assert.True(reference.Point is "DCW" or "DCWH", $"{gatherType} references unsupported model 160 point {pointReference}");
            Assert.NotNull(reference.Block);
            return;
        }
        Assert.True(SunSpecModels.Models.ContainsKey(reference.ModelId), $"{gatherType} references unknown model {reference.ModelId}");
        Assert.True(SunSpecModels.Models[reference.ModelId].Points.ContainsKey(reference.Point),
            $"{gatherType} references unknown point {pointReference}");
    }
}
