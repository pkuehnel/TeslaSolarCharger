using System;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class MeterValueLogService : TestBase
{
    public MeterValueLogService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(0, 500, 0, 300, 0, 1000,
        0, 0, 500, 300)]
    [InlineData(100, 500, 0, 300, 0, 1000,
        0, 0, 400, 300)]
    [InlineData(100, 500, 0, 0, 0, 1000,
        0, 0, 400, 0)]
    [InlineData(100, 500, 0, null, 0, 1000,
        0, 0, 400, 0)]
    [InlineData(100, 500, 0, 0, 100, 1000,
        100, 0, 300, 0)]
    [InlineData(200, 500, 0, 0, 100, 1000,
        100, 0, 200, 0)]
    [InlineData(0, 500, 0, 0, 100, 1000,
        100, 0, 400, 0)]
    [InlineData(0, 500, 0, 100, 0, 1000,
        0, 0, 500, 100)]
    [InlineData(0, 0, 100, 100, 0, 1000,
        0, 100, 0, 0)]
    [InlineData(0, 0, 1000, 0, 1000, 1000,
        0, 0, 0, 0)]
    [InlineData(0, 0, 1000, 0, 1000, -100,
        0, 0, 0, 0)]
    [InlineData(0, 0, 1000, null, null, -100,
        null, 0, 0, 0)]
    [InlineData(0, 0, 1000, null, null, null,
        null, 0, null, null)]
    [InlineData(0, null, null, null, null, null,
        null, null, null, null)]
    [InlineData(0, null, null, 0, 1000, 1000,
        0, null, 0, 0)]
    public void CanCalculateCorrectHomeBatteryPowerToGrid(int? carCombinedChargingPowerAtHome, int? fromGridMeasured, int? toGridMeasured,
        int? fromHomeBatteryMeasured, int? toHomeBatteryMeasured, int? houseMeasured,
        int? expectedHomeBatteryFromGrid, int? expectedGridFromHomeBattery, int? expectedHouseFromGrid, int? expectedHouseFromHomeBattery)
    {
        var (pvValues, fromGridValue, toGridValue, fromHomeBattery, toHomeBattery, houseConsumption) =
            GenerateTestValues(carCombinedChargingPowerAtHome, fromGridMeasured, toGridMeasured,
                fromHomeBatteryMeasured, toHomeBatteryMeasured, houseMeasured);
        var meterValueLogService = Mock.Create<TeslaSolarCharger.Server.Services.MeterValueLogService>();
        meterValueLogService.AddHomeBatteryAndGridPowers(pvValues, fromGridValue,
            toGridValue, fromHomeBattery, toHomeBattery, houseConsumption);
        if (expectedHomeBatteryFromGrid == default)
        {
            Assert.Null(toHomeBattery);
        }
        else
        {
            Assert.Equal(expectedHomeBatteryFromGrid, toHomeBattery?.MeasuredGridPower);
        }
        if (expectedGridFromHomeBattery == default)
        {
            Assert.Null(toGridValue);
        }
        else
        {
            Assert.Equal(expectedGridFromHomeBattery, toGridValue?.MeasuredHomeBatteryPower);
        }
        if (expectedHouseFromGrid == default)
        {
            Assert.Null(houseConsumption);
        }
        else
        {
            Assert.Equal(expectedHouseFromGrid, houseConsumption?.MeasuredGridPower);
        }
        if (expectedHouseFromHomeBattery == default)
        {
            Assert.Null(houseConsumption);
        }
        else
        {
            Assert.Equal(expectedHouseFromHomeBattery, houseConsumption?.MeasuredHomeBatteryPower);
        }
    }

    private (DtoPvValues pvValues, MeterValue? fromGridValue, MeterValue? toGridValue,
        MeterValue? fromHomeBattery, MeterValue? toHomeBattery, MeterValue? houseConsumption)
        GenerateTestValues(int? carCombinedChargingPowerAtHome, int? fromGridMeasured, int? toGridMeasured,
            int? fromHomeBatteryMeasured, int? toHomeBatteryMeasured, int? houseMeasured)
    {
        var pvValues = new DtoPvValues() { CarCombinedChargingPowerAtHome = carCombinedChargingPowerAtHome, };
        var fromGridValue = GenerateMeterValue(fromGridMeasured);
        var toGridValue = GenerateMeterValue(toGridMeasured);
        var fromHomeBattery = GenerateMeterValue(fromHomeBatteryMeasured);
        var toHomeBattery = GenerateMeterValue(toHomeBatteryMeasured);
        var houseConsumption = GenerateMeterValue(houseMeasured);
        return (pvValues, fromGridValue, toGridValue, fromHomeBattery, toHomeBattery, houseConsumption);
    }

    private MeterValue? GenerateMeterValue(int? measuredValue)
    {
        if (measuredValue == default)
        {
            return default;
        }
        return new MeterValue(new DateTimeOffset(2025, 8, 9, 0, 0, 0, TimeSpan.Zero), MeterValueKind.PowerToGrid, measuredValue.Value);
    }
}
