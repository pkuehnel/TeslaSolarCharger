using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services.SolarValueGathering;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// How the values read from each device are reported per device, which is what the totals and the setup readings are
/// built from, and how every value handling service is reached at once.
/// </summary>
public class GenericValueServiceTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static GenericValueService CreateService(params IGenericValue<decimal>[] values) =>
        CreateServiceWithHandlingServices(HandlingService(values).Object);

    private static GenericValueService CreateServiceWithHandlingServices(params IDecimalValueHandlingService[] handlingServices) =>
        new(NullLogger<GenericValueService>.Instance, handlingServices);

    private static Mock<IDecimalValueHandlingService> HandlingService(params IGenericValue<decimal>[] values)
    {
        var handlingService = new Mock<IDecimalValueHandlingService>();
        handlingService.Setup(s => s.GetSnapshot()).Returns(values.ToList());
        return handlingService;
    }

    [Fact]
    public void AHybridInverterBecomesOneValuePerMeasurement()
    {
        //The SMA hybrid inverter reads two solar inputs and separate charge and discharge registers. Reporting only
        //one register per measurement showed a charging battery as "0 W" and half the solar power.
        var smaHybridInverter = new TestGenericValue(new SourceValueKey(3, ConfigurationType.TemplateValue))
            .With(ValueUsage.InverterPower, 1, 2000)
            .With(ValueUsage.InverterPower, 2, 1000)
            .With(ValueUsage.HomeBatteryPower, 3, 0)
            .With(ValueUsage.HomeBatteryPower, 4, 1500)
            .With(ValueUsage.HomeBatterySoc, 5, 55);

        var sourceValues = CreateService(smaHybridInverter).GetSourceValues(true);

        Assert.Equal(3, sourceValues.Count);
        Assert.All(sourceValues, v =>
        {
            Assert.Equal(ConfigurationType.TemplateValue, v.ConfigurationType);
            Assert.Equal(3, v.SourceId);
        });
        Assert.Equal(3000, sourceValues.Single(v => v.UsedFor == ValueUsage.InverterPower).Value.Value);
        Assert.Equal(1500, sourceValues.Single(v => v.UsedFor == ValueUsage.HomeBatteryPower).Value.Value);
        Assert.Equal(55, sourceValues.Single(v => v.UsedFor == ValueUsage.HomeBatterySoc).Value.Value);
    }

    [Fact]
    public void EveryDeviceKeepsItsOwnEntry()
    {
        var energyMeter = new TestGenericValue(new SourceValueKey(1, ConfigurationType.ModbusSolarValue))
            .With(ValueUsage.GridPower, 1, -400);
        var secondMeter = new TestGenericValue(new SourceValueKey(1, ConfigurationType.RestSolarValue))
            .With(ValueUsage.GridPower, 1, 100);

        var sourceValues = CreateService(energyMeter, secondMeter).GetSourceValues(true);

        Assert.Equal(2, sourceValues.Count);
        Assert.Equal(-400, sourceValues.Single(v => v.ConfigurationType == ConfigurationType.ModbusSolarValue).Value.Value);
        Assert.Equal(100, sourceValues.Single(v => v.ConfigurationType == ConfigurationType.RestSolarValue).Value.Value);
    }

    [Fact]
    public void TheDevicesOfEveryHandlingServiceAreReported()
    {
        var mqttMeter = new TestGenericValue(new SourceValueKey(1, ConfigurationType.MqttSolarValue))
            .With(ValueUsage.GridPower, 1, -400);
        var modbusInverter = new TestGenericValue(new SourceValueKey(1, ConfigurationType.ModbusSolarValue))
            .With(ValueUsage.InverterPower, 1, 2500);
        var service = CreateServiceWithHandlingServices(HandlingService(mqttMeter).Object, HandlingService(modbusInverter).Object);

        var sourceValues = service.GetSourceValues(true);

        Assert.Equal(2, sourceValues.Count);
        Assert.Contains(sourceValues, v => v.ConfigurationType == ConfigurationType.MqttSolarValue && v.Value.Value == -400);
        Assert.Contains(sourceValues, v => v.ConfigurationType == ConfigurationType.ModbusSolarValue && v.Value.Value == 2500);
    }

    [Fact]
    public void TheNewestReadingOfADeviceIsItsTimestamp()
    {
        var device = new TestGenericValue(new SourceValueKey(1, ConfigurationType.TemplateValue))
            .With(ValueUsage.HomeBatteryPower, 3, 0, ReadAt.AddSeconds(-5))
            .With(ValueUsage.HomeBatteryPower, 4, 1500, ReadAt);

        var sourceValue = Assert.Single(CreateService(device).GetSourceValues(true));

        Assert.Equal(ReadAt, sourceValue.Value.Timestamp);
    }

    [Fact]
    public void CarValuesAreLeftOut()
    {
        var device = new TestGenericValue(new SourceValueKey(1, ConfigurationType.TemplateValue))
            .With(ValueUsage.GridPower, 2, 300)
            .WithCarValue(3, 80);

        var sourceValue = Assert.Single(CreateService(device).GetSourceValues(true));

        Assert.Equal(ValueUsage.GridPower, sourceValue.UsedFor);
        Assert.Equal(300, sourceValue.Value.Value);
    }

    [Fact]
    public void ADeviceWithoutSolarGridOrBatteryReadingsIsLeftOut()
    {
        var carOnly = new TestGenericValue(new SourceValueKey(1, ConfigurationType.CarValue)).WithCarValue(1, 80);

        Assert.Empty(CreateService(carOnly).GetSourceValues(false));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public void ADeviceWithAnErrorIsOnlyLeftOutWhenAskedTo(bool skipValuesWithError, int expectedCount)
    {
        var working = new TestGenericValue(new SourceValueKey(1, ConfigurationType.TemplateValue))
            .With(ValueUsage.GridPower, 1, 300);
        var failing = new TestGenericValue(new SourceValueKey(2, ConfigurationType.TemplateValue), hasError: true)
            .With(ValueUsage.GridPower, 1, 999);

        var sourceValues = CreateService(working, failing).GetSourceValues(skipValuesWithError);

        Assert.Equal(expectedCount, sourceValues.Count);
        Assert.Contains(sourceValues, v => v.SourceId == 1);
    }

    [Fact]
    public void NoDevicesMeansNoValues()
    {
        Assert.Empty(CreateService().GetSourceValues(true));
        Assert.Empty(CreateServiceWithHandlingServices().GetSourceValues(true));
    }

    [Fact]
    public async Task RecreatingValuesReachesEveryHandlingService()
    {
        //Startup recreates every value through this one call.
        var first = HandlingService();
        var second = HandlingService();
        var service = CreateServiceWithHandlingServices(first.Object, second.Object);

        await service.RecreateValues(null);

        first.Verify(s => s.RecreateValues(null, It.Is<List<int>>(ids => ids.Count == 0)), Times.Once);
        second.Verify(s => s.RecreateValues(null, It.Is<List<int>>(ids => ids.Count == 0)), Times.Once);
    }

    private sealed class TestGenericValue(SourceValueKey sourceValueKey, bool hasError = false) : IGenericValue<decimal>
    {
        private readonly Dictionary<ValueKey, DtoHistoricValue<decimal>> _historicValues = new();

        public TestGenericValue With(ValueUsage usage, int resultConfigurationId, decimal value, DateTimeOffset? timestamp = null)
        {
            _historicValues[new ValueKey(usage, null, resultConfigurationId)] = new(timestamp ?? ReadAt, value, 10);
            return this;
        }

        public TestGenericValue WithCarValue(int resultConfigurationId, decimal value)
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
