using System;
using System.Collections.Generic;
using System.Linq;
using TeslaSolarCharger.Shared.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class AddChargingScheduleSplitTests : TestBase
{
    private const int MaxPower = 11_000;
    private const int HomeBatPower = 4_500;

    public AddChargingScheduleSplitTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    private DtoChargingSchedule CreateSchedule(
        DateTimeOffset from,
        DateTimeOffset to,
        int targetMinPower,
        int? targetHomeBatteryPower,
        ScheduleReason reason)
    {
        return new DtoChargingSchedule(1, null, MaxPower, new HashSet<ScheduleReason> { reason })
        {
            ValidFrom = from,
            ValidTo = to,
            TargetMinPower = targetMinPower,
            TargetHomeBatteryPower = targetHomeBatteryPower,
            MaxPossiblePower = MaxPower,
        };
    }

    [Theory]
    [InlineData(true, "StartHigh")]
    [InlineData(false, "EndHigh")]
    public void AddChargingSchedule_PartialOverlapEnergy_ShouldSplitScheduleCorrectly(bool contiguousWithPrevious, string expectedHighPowerPosition)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var start = CurrentFakeDate;
        var end = CurrentFakeDate.AddHours(1);

        // Create existing schedule list
        var existingSchedules = new List<DtoChargingSchedule>();

        // If contiguous, we add a dummy schedule BEFORE the target slot
        if (contiguousWithPrevious)
        {
            existingSchedules.Add(CreateSchedule(start.AddHours(-1), start, 0, 0, ScheduleReason.LatestPossibleTime));
        }

        // Existing overlapping schedule: HomeBatteryDischarging (4500W)
        var existingSchedule = CreateSchedule(start, end, 0, HomeBatPower, ScheduleReason.HomeBatteryDischarging);
        existingSchedules.Add(existingSchedule);

        // New: CheapGridPrice (11000W) overlapping
        // We set TargetMinPower to MaxPower.
        var newSchedule = CreateSchedule(start, end, MaxPower, null, ScheduleReason.CheapGridPrice);

        // We want to add only 1000Wh of Grid energy.
        // Full grid boost would provide (11000 - 4500) * 1h = 6500Wh.
        // We limit to 1000Wh.
        int maxEnergyToAdd = 1000;
        double expectedDuration = 1000.0 / 6500.0; // 1000Wh / 6500W

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd);

        // Assert
        Assert.Equal(maxEnergyToAdd, addedEnergy);

        // Identify the schedules in the target window [start, end]
        // Note: Floating point comparison for dates might be tricky but DateTimeOffset exact match should work here as created from same variable
        var schedulesInWindow = schedules.Where(s => s.ValidFrom >= start && s.ValidTo <= end).OrderBy(s => s.ValidFrom).ToList();

        // Should be split into 2 parts
        Assert.Equal(2, schedulesInWindow.Count);

        var first = schedulesInWindow[0];
        var second = schedulesInWindow[1];

        // Check continuity
        Assert.Equal(start, first.ValidFrom);
        Assert.Equal(first.ValidTo, second.ValidFrom);
        Assert.Equal(end, second.ValidTo);

        if (expectedHighPowerPosition == "StartHigh")
        {
            // First part should be High Power
            Assert.Equal(MaxPower, first.TargetMinPower);
            Assert.Equal(0, second.TargetMinPower); // Home Bat Only (TargetMinPower=0)

            // Duration check
            Assert.Equal(expectedDuration, (first.ValidTo - first.ValidFrom).TotalHours, 2);
        }
        else // EndHigh
        {
            // Second part should be High Power
            Assert.Equal(0, first.TargetMinPower);
            Assert.Equal(MaxPower, second.TargetMinPower);

            // Duration check
            Assert.Equal(expectedDuration, (second.ValidTo - second.ValidFrom).TotalHours, 2);
        }
    }
}
