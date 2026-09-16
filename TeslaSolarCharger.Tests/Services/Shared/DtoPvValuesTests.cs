using System;
using System.Collections.Generic;
using System.Text.Json;
using TeslaSolarCharger.Client.Services;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

/// <summary>
/// The totals the start page, setup and the charging logic work with, worked out from what each device delivered.
/// </summary>
public class DtoPvValuesTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static DtoPvSourceValue Source(ValueUsage usage, decimal value, int sourceId = 1,
        ConfigurationType configurationType = ConfigurationType.TemplateValue) => new()
    {
        ConfigurationType = configurationType,
        SourceId = sourceId,
        UsedFor = usage,
        Value = value,
        LastUpdated = ReadAt,
    };

    [Fact]
    public void AMeasurementNoDeviceSuppliesHasNoTotal()
    {
        //Null tells "not measured" apart from "measured as zero", which the charging logic and the pages rely on.
        var pvValues = new DtoPvValues();

        Assert.Null(pvValues.InverterPower);
        Assert.Null(pvValues.GridPower);
        Assert.Null(pvValues.HomeBatteryPower);
        Assert.Null(pvValues.HomeBatterySoc);
    }

    [Fact]
    public void ADeviceReportingZeroIsMeasuredAsZero()
    {
        var pvValues = new DtoPvValues { SourceValues = { Source(ValueUsage.HomeBatteryPower, 0), }, };

        Assert.Equal(0, pvValues.HomeBatteryPower);
    }

    [Fact]
    public void TheValuesOfSeveralDevicesAreAddedUp()
    {
        var pvValues = new DtoPvValues
        {
            SourceValues =
            {
                Source(ValueUsage.InverterPower, 3000, sourceId: 1),
                Source(ValueUsage.InverterPower, 1200, sourceId: 1, configurationType: ConfigurationType.ModbusSolarValue),
                Source(ValueUsage.InverterPower, 800, sourceId: 2),
            },
        };

        Assert.Equal(5000, pvValues.InverterPower);
    }

    [Fact]
    public void EachMeasurementOnlyAddsUpItsOwnValues()
    {
        var pvValues = new DtoPvValues
        {
            SourceValues =
            {
                Source(ValueUsage.InverterPower, 4000),
                Source(ValueUsage.GridPower, -250),
                Source(ValueUsage.HomeBatteryPower, 1500),
                Source(ValueUsage.HomeBatterySoc, 63),
            },
        };

        Assert.Equal(4000, pvValues.InverterPower);
        Assert.Equal(-250, pvValues.GridPower);
        Assert.Equal(1500, pvValues.HomeBatteryPower);
        Assert.Equal(63, pvValues.HomeBatterySoc);
    }

    [Theory]
    //An inverter drawing standby power at night is not negative solar generation.
    [InlineData(ValueUsage.InverterPower, -30, 0)]
    //Grid import and battery discharge are negative on purpose and must keep their sign.
    [InlineData(ValueUsage.GridPower, -30, -30)]
    [InlineData(ValueUsage.HomeBatteryPower, -30, -30)]
    public void OnlySolarGenerationIsNeverNegative(ValueUsage usage, int value, int expectedTotal)
    {
        var pvValues = new DtoPvValues { SourceValues = { Source(usage, value), }, };

        Assert.Equal(expectedTotal, pvValues.ValueFor(usage));
    }

    [Theory]
    [InlineData(100.9, 100)]
    [InlineData(-100.9, -100)]
    public void FractionsOfTheTotalAreCutOff(double value, int expectedTotal)
    {
        var pvValues = new DtoPvValues { SourceValues = { Source(ValueUsage.GridPower, (decimal)value), }, };

        Assert.Equal(expectedTotal, pvValues.GridPower);
    }

    [Fact]
    public void FractionsAreCutOffAfterAddingUpNotPerDevice()
    {
        //Two devices each delivering half a watt make one watt, not zero.
        var pvValues = new DtoPvValues
        {
            SourceValues = { Source(ValueUsage.GridPower, 0.5m, sourceId: 1), Source(ValueUsage.GridPower, 0.5m, sourceId: 2), },
        };

        Assert.Equal(1, pvValues.GridPower);
    }

    [Theory]
    [InlineData(ValueUsage.GridPower, 1e12, int.MaxValue)]
    [InlineData(ValueUsage.GridPower, -1e12, int.MinValue)]
    public void AnImpossiblyLargeTotalIsClampedInsteadOfOverflowing(ValueUsage usage, double value, int expectedTotal)
    {
        var pvValues = new DtoPvValues { SourceValues = { Source(usage, (decimal)value), }, };

        Assert.Equal(expectedTotal, pvValues.ValueFor(usage));
    }

    [Fact]
    public void TheTotalsSurviveTheInitialStateSentToThePages()
    {
        //The initial state is sent as the whole object, serialized with the default settings.
        var original = new DtoPvValues
        {
            SourceValues = { Source(ValueUsage.HomeBatteryPower, 1500, sourceId: 7), },
            PowerBuffer = 100,
            LastUpdated = ReadAt,
        };

        var received = JsonSerializer.Deserialize<DtoPvValues>(JsonSerializer.Serialize(original))!;

        Assert.Equal(1500, received.HomeBatteryPower);
        var sourceValue = Assert.Single(received.SourceValues);
        Assert.Equal(7, sourceValue.SourceId);
        Assert.Equal(ConfigurationType.TemplateValue, sourceValue.ConfigurationType);
        Assert.Equal(ReadAt, sourceValue.LastUpdated);
    }

    [Fact]
    public void TheDeviceValuesSurviveAChangeSentInCamelCase()
    {
        //SignalR writes the fields of a changed list in camel case. Read case-sensitively, every device would arrive
        //with a value of zero.
        var sourceValues = new List<DtoPvSourceValue> { Source(ValueUsage.HomeBatteryPower, 1500, sourceId: 7), };
        var sentBySignalR = JsonSerializer.Serialize(sourceValues, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, });

        var received = JsonSerializer.Deserialize<List<DtoPvSourceValue>>(sentBySignalR,
            SignalRStateService.ChangedPropertySerializerOptions)!;

        var sourceValue = Assert.Single(received);
        Assert.Equal(1500, sourceValue.Value);
        Assert.Equal(7, sourceValue.SourceId);
        Assert.Equal(ValueUsage.HomeBatteryPower, sourceValue.UsedFor);
    }
}
