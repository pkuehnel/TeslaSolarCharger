using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// How the values read from each device are reported per device, which is what the totals and the setup readings are
/// built from.
/// </summary>
public class DecimalValueHandlingServiceSourceValuesTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly HashSet<ValueUsage> AllUsages =
    [
        ValueUsage.InverterPower,
        ValueUsage.GridPower,
        ValueUsage.HomeBatteryPower,
        ValueUsage.HomeBatterySoc,
    ];

    [Fact]
    public void AHybridInverterBecomesOneValuePerMeasurement()
    {
        //The SMA hybrid inverter reads two solar inputs and separate charge and discharge registers. Reporting only
        //one register per measurement showed a charging battery as "0 W" and half the solar power.
        var smaHybridInverter = new FakeGenericValue(new SourceValueKey(3, ConfigurationType.TemplateValue))
            .With(ValueUsage.InverterPower, 1, 2000)
            .With(ValueUsage.InverterPower, 2, 1000)
            .With(ValueUsage.HomeBatteryPower, 3, 0)
            .With(ValueUsage.HomeBatteryPower, 4, 1500)
            .With(ValueUsage.HomeBatterySoc, 5, 55);
        var service = new TestValueHandlingService(smaHybridInverter);

        var sourceValues = service.GetSourceValues(AllUsages, true);

        Assert.Equal(3, sourceValues.Count);
        Assert.All(sourceValues, v =>
        {
            Assert.Equal(ConfigurationType.TemplateValue, v.ConfigurationType);
            Assert.Equal(3, v.SourceId);
        });
        Assert.Equal(3000, sourceValues.Single(v => v.UsedFor == ValueUsage.InverterPower).Value);
        Assert.Equal(1500, sourceValues.Single(v => v.UsedFor == ValueUsage.HomeBatteryPower).Value);
        Assert.Equal(55, sourceValues.Single(v => v.UsedFor == ValueUsage.HomeBatterySoc).Value);
    }

    [Fact]
    public void EveryDeviceKeepsItsOwnEntry()
    {
        var energyMeter = new FakeGenericValue(new SourceValueKey(1, ConfigurationType.ModbusSolarValue))
            .With(ValueUsage.GridPower, 1, -400);
        var secondMeter = new FakeGenericValue(new SourceValueKey(1, ConfigurationType.RestSolarValue))
            .With(ValueUsage.GridPower, 1, 100);
        var service = new TestValueHandlingService(energyMeter, secondMeter);

        var sourceValues = service.GetSourceValues(AllUsages, true);

        Assert.Equal(2, sourceValues.Count);
        Assert.Equal(-400, sourceValues.Single(v => v.ConfigurationType == ConfigurationType.ModbusSolarValue).Value);
        Assert.Equal(100, sourceValues.Single(v => v.ConfigurationType == ConfigurationType.RestSolarValue).Value);
    }

    [Fact]
    public void TheNewestReadingOfADeviceIsItsLastUpdate()
    {
        var device = new FakeGenericValue(new SourceValueKey(1, ConfigurationType.TemplateValue))
            .With(ValueUsage.HomeBatteryPower, 3, 0, ReadAt.AddSeconds(-5))
            .With(ValueUsage.HomeBatteryPower, 4, 1500, ReadAt);
        var service = new TestValueHandlingService(device);

        var sourceValue = Assert.Single(service.GetSourceValues(AllUsages, true));

        Assert.Equal(ReadAt, sourceValue.LastUpdated);
    }

    [Fact]
    public void OnlyTheRequestedMeasurementsAreReported()
    {
        var device = new FakeGenericValue(new SourceValueKey(1, ConfigurationType.TemplateValue))
            .With(ValueUsage.InverterPower, 1, 2000)
            .With(ValueUsage.GridPower, 2, 300)
            .WithCarValue(3, 80);
        var service = new TestValueHandlingService(device);

        var sourceValue = Assert.Single(service.GetSourceValues([ValueUsage.GridPower,], true));

        Assert.Equal(ValueUsage.GridPower, sourceValue.UsedFor);
        Assert.Equal(300, sourceValue.Value);
    }

    [Fact]
    public void ADeviceWithoutMatchingReadingsIsLeftOut()
    {
        var carOnly = new FakeGenericValue(new SourceValueKey(1, ConfigurationType.CarValue)).WithCarValue(1, 80);
        var service = new TestValueHandlingService(carOnly);

        Assert.Empty(service.GetSourceValues(AllUsages, false));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public void ADeviceWithAnErrorIsOnlyLeftOutWhenAskedTo(bool skipValuesWithError, int expectedCount)
    {
        var working = new FakeGenericValue(new SourceValueKey(1, ConfigurationType.TemplateValue))
            .With(ValueUsage.GridPower, 1, 300);
        var failing = new FakeGenericValue(new SourceValueKey(2, ConfigurationType.TemplateValue), hasError: true)
            .With(ValueUsage.GridPower, 1, 999);
        var service = new TestValueHandlingService(working, failing);

        var sourceValues = service.GetSourceValues(AllUsages, skipValuesWithError);

        Assert.Equal(expectedCount, sourceValues.Count);
        Assert.Contains(sourceValues, v => v.SourceId == 1);
    }

    [Fact]
    public void NoDevicesMeansNoValues()
    {
        Assert.Empty(new TestValueHandlingService().GetSourceValues(AllUsages, true));
    }

    private sealed class TestValueHandlingService : DecimalValueHandlingServiceBase<IGenericValue<decimal>>
    {
        public TestValueHandlingService(params IGenericValue<decimal>[] values)
            : base(new Mock<IServiceScopeFactory>().Object)
        {
            AddGenericValues(values);
        }

        public override Task RecreateValues(ConfigurationType? configurationType, params List<int> configurationIds) =>
            Task.CompletedTask;
    }

    private sealed class FakeGenericValue(SourceValueKey sourceValueKey, bool hasError = false) : IGenericValue<decimal>
    {
        private readonly Dictionary<ValueKey, DtoHistoricValue<decimal>> _historicValues = new();

        public FakeGenericValue With(ValueUsage usage, int resultConfigurationId, decimal value, DateTimeOffset? timestamp = null)
        {
            _historicValues[new ValueKey(usage, null, resultConfigurationId)] = new(timestamp ?? ReadAt, value, 10);
            return this;
        }

        public FakeGenericValue WithCarValue(int resultConfigurationId, decimal value)
        {
            _historicValues[new ValueKey(null, CarValueType.StateOfCharge, resultConfigurationId)] = new(ReadAt, value, 10);
            return this;
        }

        public SourceValueKey SourceValueKey => sourceValueKey;
        public IReadOnlyDictionary<ValueKey, DtoHistoricValue<decimal>> HistoricValues => _historicValues;
        public void UpdateValue(ValueKey valueKey, DateTimeOffset timestamp, decimal value) => throw new NotSupportedException();
        public string? ErrorMessage => hasError ? "Device did not answer" : null;
        public string? ErrorStackTrace => null;
        public DateTimeOffset? HasErrorSince => hasError ? ReadAt : null;
        public bool HasError => hasError;
        public void Cancel()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
