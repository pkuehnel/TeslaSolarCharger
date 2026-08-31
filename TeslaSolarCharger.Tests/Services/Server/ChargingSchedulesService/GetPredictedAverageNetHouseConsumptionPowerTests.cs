using System;
using System.Collections.Generic;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class GetPredictedAverageNetHouseConsumptionPowerTests : TestBase
{
    public GetPredictedAverageNetHouseConsumptionPowerTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void ReturnsConsumption_WhenAllSlicesHaveNegativeSurplus()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var from = CurrentFakeDate;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { from, -1000 },
            { from.AddHours(1), -1000 },
        };

        var result = service.GetPredictedAverageNetHouseConsumptionPower(slices, from, from.AddHours(2));

        Assert.Equal(1000, result);
    }

    [Fact]
    public void WeightsSlicesByOverlapWithTimeRange()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var sliceStart = CurrentFakeDate;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { sliceStart, -600 },
            { sliceStart.AddHours(1), -1200 },
        };

        // Half an hour of each slice overlaps the range => (600 * 0.5 + 1200 * 0.5) / 1 = 900
        var result = service.GetPredictedAverageNetHouseConsumptionPower(slices, sliceStart.AddMinutes(30), sliceStart.AddMinutes(90));

        Assert.Equal(900, result);
    }

    [Fact]
    public void ReturnsZero_WhenAllSlicesHaveSurplus()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var from = CurrentFakeDate;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { from, 400 },
            { from.AddHours(1), 1000 },
        };

        var result = service.GetPredictedAverageNetHouseConsumptionPower(slices, from, from.AddHours(2));

        Assert.Equal(0, result);
    }

    /// <summary>
    /// A predicted surplus covers the house consumption of its own slice but must never offset the consumption of
    /// other slices: that energy already reaches the car via solar based schedules and via the power to control at
    /// execution time, so crediting it here would credit it twice and overestimate the discharge power reaching the car.
    /// </summary>
    [Theory]
    //-1000Wh and +400Wh over two hours: only the consumption counts => 1000 / 2 = 500W
    [InlineData(-1000, 400, 500)]
    //Even when the surplus dominates, the consumption of the other slice stays uncovered => 400 / 2 = 200W
    [InlineData(-400, 1000, 200)]
    public void SurplusSlices_DoNotOffsetConsumptionOfOtherSlices(int firstSliceEnergy, int secondSliceEnergy,
        int expectedConsumptionPower)
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var from = CurrentFakeDate;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { from, firstSliceEnergy },
            { from.AddHours(1), secondSliceEnergy },
        };

        var result = service.GetPredictedAverageNetHouseConsumptionPower(slices, from, from.AddHours(2));

        Assert.Equal(expectedConsumptionPower, result);
    }

    [Fact]
    public void ReturnsZero_WhenNoSlicesAvailable()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var result = service.GetPredictedAverageNetHouseConsumptionPower(new(), CurrentFakeDate, CurrentFakeDate.AddHours(2));

        Assert.Equal(0, result);
    }

    [Fact]
    public void ReturnsZero_WhenNoSliceOverlapsTimeRange()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate.AddHours(-5), -1000 },
        };

        var result = service.GetPredictedAverageNetHouseConsumptionPower(slices, CurrentFakeDate, CurrentFakeDate.AddHours(2));

        Assert.Equal(0, result);
    }

    [Fact]
    public void ReturnsZero_WhenTimeRangeIsEmpty()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, -1000 },
        };

        var result = service.GetPredictedAverageNetHouseConsumptionPower(slices, CurrentFakeDate, CurrentFakeDate);

        Assert.Equal(0, result);
    }
}
