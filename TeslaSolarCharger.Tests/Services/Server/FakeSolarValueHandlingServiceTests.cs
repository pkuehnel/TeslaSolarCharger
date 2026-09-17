using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Fake;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// The device the made-up solar values live in, so they are read exactly like the values of a real device.
/// </summary>
public class FakeSolarValueHandlingServiceTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeSolarValueHandlingService _service = new(new Mock<IServiceScopeFactory>().Object);

    private IGenericValue<decimal> FakeDevice => Assert.Single(_service.GetSnapshot());

    private static Dictionary<ValueUsage, int?> Values(int? inverterPower, int? gridPower, int? homeBatteryPower, int? homeBatterySoc) => new()
    {
        { ValueUsage.InverterPower, inverterPower },
        { ValueUsage.GridPower, gridPower },
        { ValueUsage.HomeBatteryPower, homeBatteryPower },
        { ValueUsage.HomeBatterySoc, homeBatterySoc },
    };

    private decimal? ValueOf(ValueUsage usage) => FakeDevice.HistoricValues
        .Where(v => v.Key.ValueUsage == usage)
        .Select(v => (decimal?)v.Value.Value)
        .SingleOrDefault();

    [Fact]
    public void TheFakeDeviceIsThereButDeliversNothingUntilValuesAreSet()
    {
        //Fake solar values are switched off in every real installation, where the device must not add anything.
        var device = FakeDevice;

        Assert.Equal(new SourceValueKey(0, ConfigurationType.FakeSolarValue), device.SourceValueKey);
        Assert.Empty(device.HistoricValues);
        Assert.False(device.HasError);
    }

    [Fact]
    public void EverySetMeasurementIsDeliveredWithItsTimestamp()
    {
        _service.SetValues(ReadAt, Values(1000, -300, 500, 20));

        Assert.Equal(1000, ValueOf(ValueUsage.InverterPower));
        Assert.Equal(-300, ValueOf(ValueUsage.GridPower));
        Assert.Equal(500, ValueOf(ValueUsage.HomeBatteryPower));
        Assert.Equal(20, ValueOf(ValueUsage.HomeBatterySoc));
        Assert.All(FakeDevice.HistoricValues.Values, v => Assert.Equal(ReadAt, v.Timestamp));
    }

    [Fact]
    public void AMeasurementLeftOutIsNotDeliveredAsZero()
    {
        _service.SetValues(ReadAt, Values(null, 200, null, null));

        var reading = Assert.Single(FakeDevice.HistoricValues);
        Assert.Equal(ValueUsage.GridPower, reading.Key.ValueUsage);
    }

    [Fact]
    public void AMeasurementLeftOutLaterNoLongerDeliversItsOldValue()
    {
        //The demo cases switch measurements on and off; a battery from the case before must not stay behind.
        _service.SetValues(ReadAt, Values(1000, 300, 500, 20));

        _service.SetValues(ReadAt.AddSeconds(5), Values(null, 200, null, null));

        Assert.Null(ValueOf(ValueUsage.InverterPower));
        Assert.Null(ValueOf(ValueUsage.HomeBatteryPower));
        Assert.Null(ValueOf(ValueUsage.HomeBatterySoc));
        Assert.Equal(200, ValueOf(ValueUsage.GridPower));
    }

    [Fact]
    public void NewValuesReplaceTheOldOnes()
    {
        _service.SetValues(ReadAt, Values(1000, 300, null, null));

        _service.SetValues(ReadAt.AddSeconds(5), Values(10, -500, null, null));

        Assert.Equal(10, ValueOf(ValueUsage.InverterPower));
        Assert.Equal(-500, ValueOf(ValueUsage.GridPower));
        Assert.All(FakeDevice.HistoricValues.Values, v => Assert.Equal(ReadAt.AddSeconds(5), v.Timestamp));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ConfigurationType.FakeSolarValue)]
    [InlineData(ConfigurationType.TemplateValue)]
    public async Task RecreatingValuesKeepsTheFakeDeviceAndItsValues(ConfigurationType? configurationType)
    {
        //Startup and every saved configuration recreate values, but the fake device is not configured anywhere.
        _service.SetValues(ReadAt, Values(1000, null, null, null));

        await _service.RecreateValues(configurationType);

        Assert.Equal(1000, ValueOf(ValueUsage.InverterPower));
    }
}
