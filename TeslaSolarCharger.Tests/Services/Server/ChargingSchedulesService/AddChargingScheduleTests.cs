using System;
using System.Collections.Generic;
using System.Linq;
using TeslaSolarCharger.Shared.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class AddChargingScheduleTests : TestBase
{
    private const int MaxPower = 11_040;

    public AddChargingScheduleTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    // --- HELPER METHOD TO REDUCE DUPLICATION ---
    private DtoChargingSchedule CreateSchedule(
        DateTimeOffset from,
        DateTimeOffset to,
        int targetPower,
        int maxPower = MaxPower,
        ScheduleReason reason = ScheduleReason.LatestPossibleTime)
    {
        return new DtoChargingSchedule(1, null, maxPower, new() { reason })
        {
            ValidFrom = from,
            ValidTo = to,
            TargetMinPower = targetPower,
            MaxPossiblePower = maxPower
        };
    }

    [Theory]
    [MemberData(nameof(GetPartialOverlapTestData))]
    public void AddChargingSchedule_PartialOverlap_SplitsSchedules(
        int existingPower,
        int newTargetPower,
        int maxPower,
        double overlapDurationHours,
        double boostDurationHours,
        string testDescription)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var start = CurrentFakeDate;
        var end = CurrentFakeDate.AddHours(overlapDurationHours); // e.g. 1 hour total duration

        // Existing schedule (e.g., Home Battery discharge)
        var existingSchedule = CreateSchedule(start, end, existingPower, maxPower, ScheduleReason.HomeBatteryDischarging);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };

        // New schedule request (e.g., Grid boost) for the same duration, but we will limit energy
        var newSchedule = CreateSchedule(start, end, newTargetPower, maxPower, ScheduleReason.LatestPossibleTime);

        // Calculate Max Energy to Add:
        // We only want to add enough energy for 'boostDurationHours' at the DIFFERENCE in power.
        // E.g. (11000 - 4500) * 0.166h
        var powerDifference = newTargetPower - existingPower;
        var maxEnergyToAdd = (int)(powerDifference * boostDurationHours);
        // Add a small epsilon to avoid rounding issues causing it to be slightly under
        maxEnergyToAdd += 1;

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, maxPower, maxEnergyToAdd);

        // Assert
        // We expect the schedule to be SPLIT into TWO parts:
        // 1. One part at existingPower (Base)
        // 2. One part at newTargetPower (Boosted)
        // Total duration should still be 'overlapDurationHours' (approximately, due to float math)

        Assert.True(schedules.Count >= 2, $"Should have split the schedule into at least 2 parts (Base and Boost) for test '{testDescription}'.");

        // Verify we have a Boosted segment
        var boostedSegment = schedules.FirstOrDefault(s => s.TargetMinPower >= newTargetPower);
        Assert.NotNull(boostedSegment); // Fail if no segment has the high power

        // Verify we have a Base segment
        var baseSegment = schedules.FirstOrDefault(s => s.TargetMinPower == existingPower);
        Assert.NotNull(baseSegment);

        // Verify duration of boosted segment is close to expected
        var actualBoostDuration = (boostedSegment.ValidTo - boostedSegment.ValidFrom).TotalHours;
        Assert.InRange(actualBoostDuration, boostDurationHours - 0.05, boostDurationHours + 0.05);

        // Verify total energy added
        Assert.InRange(addedEnergy, maxEnergyToAdd - 10, maxEnergyToAdd + 10);
    }

    public static IEnumerable<object[]> GetPartialOverlapTestData()
    {
        // Scenario 1: User reported case
        // 4500W base, 11000W boost, 1 hour total, ~10 mins boost (0.166h)
        yield return new object[] { 4500, 11000, 11088, 1.0, 0.166, "User Scenario: 4.5kW Base, 11kW Boost, 10min fill" };

        // Scenario 2: Half hour boost
        yield return new object[] { 4500, 11000, 11088, 1.0, 0.5, "Half Hour Boost" };

        // Scenario 3: Different power levels
        yield return new object[] { 2000, 10000, 11000, 2.0, 0.25, "Low Base, High Boost, 15min fill in 2h slot" };
    }


    [Fact]
    public void AddChargingSchedule_EmptyList_AddsFullSchedule()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var existingSchedules = new List<DtoChargingSchedule>();
        var start = CurrentFakeDate.AddHours(1);
        var end = CurrentFakeDate.AddHours(2);
        var newSchedule = CreateSchedule(start, end, MaxPower);

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower);

        Assert.Single(schedules);
        Assert.Equal(MaxPower, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_FullOverlap_IncreasesExistingPower()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var start = CurrentFakeDate.AddHours(1);
        var end = CurrentFakeDate.AddHours(2);
        var existingSchedule = CreateSchedule(start, end, MaxPower / 2, MaxPower, ScheduleReason.HomeBatteryDischarging);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };
        var newSchedule = CreateSchedule(start, end, MaxPower, MaxPower, ScheduleReason.LatestPossibleTime);

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower);

        Assert.Single(schedules);
        var result = schedules.First();
        Assert.Equal(MaxPower, result.TargetMinPower);
        Assert.Equal(MaxPower / 2, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_FullOverlap_AlreadyMaxPower_NoChange()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var start = CurrentFakeDate.AddHours(1);
        var end = CurrentFakeDate.AddHours(2);
        var existingSchedule = CreateSchedule(start, end, MaxPower);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };
        var newSchedule = CreateSchedule(start, end, MaxPower);

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower);

        Assert.Single(schedules);
        var result = schedules.First();
        Assert.Equal(MaxPower, result.TargetMinPower);
        Assert.Equal(0, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialOverlap_IncreasesPowerOnOverlap_AndAddsNewSlot()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = CurrentFakeDate.AddHours(1);
        var t3 = CurrentFakeDate.AddHours(2);

        // Existing: 10:00 - 12:00 @ 50% Power
        // The splitter will likely split this into 10-11 and 11-12
        var existingSchedule = CreateSchedule(t1, t3, MaxPower / 2);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };

        // New: 11:00 - 12:00 @ 100% Power (Overlaps the second half of existing)
        var newSchedule = CreateSchedule(t2, t3, MaxPower);

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower);

        Assert.Equal(2, schedules.Count);

        var segment1 = schedules.FirstOrDefault(s => s.ValidFrom == t1);
        Assert.NotNull(segment1);
        Assert.Equal(MaxPower / 2, segment1.TargetMinPower);

        var segment2 = schedules.FirstOrDefault(s => s.ValidFrom == t2);
        Assert.NotNull(segment2);
        Assert.Equal(MaxPower, segment2.TargetMinPower);
        Assert.Equal(MaxPower / 2, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialOverlap_AlreadyMax_NoPowerChange_ButStructureUpdate()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = CurrentFakeDate.AddHours(1);
        var t3 = CurrentFakeDate.AddHours(2);

        var existingSchedule = CreateSchedule(t1, t3, MaxPower);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };
        var newSchedule = CreateSchedule(t2, t3, MaxPower);

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower);

        Assert.Equal(2, schedules.Count);
        var segment2 = schedules.FirstOrDefault(s => s.ValidFrom == t2);
        Assert.NotNull(segment2);
        Assert.Equal(MaxPower, segment2.TargetMinPower);
        Assert.Equal(0, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_ComplexOverlap_InsertsNewSlot_AndUpgradesExisting()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t10 = CurrentFakeDate;
        var t11 = CurrentFakeDate.AddHours(1);
        var t12 = CurrentFakeDate.AddHours(2);

        var existing = CreateSchedule(t10, t11, MaxPower / 2);
        var existingSchedules = new List<DtoChargingSchedule> { existing };

        var t10_30 = t10.AddMinutes(30);
        var newSchedule = CreateSchedule(t10_30, t12, MaxPower);

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower * 10);

        Assert.Equal(3, schedules.Count);

        var part1 = schedules.Single(s => s.ValidFrom == t10);
        Assert.Equal(t10_30, part1.ValidTo);
        Assert.Equal(MaxPower / 2, part1.TargetMinPower);

        var part2 = schedules.Single(s => s.ValidFrom == t10_30 && s.ValidTo == t11);
        Assert.Equal(MaxPower, part2.TargetMinPower);

        var part3 = schedules.Single(s => s.ValidFrom == t11);
        Assert.Equal(t12, part3.ValidTo);
        Assert.Equal(MaxPower, part3.TargetMinPower);
    }

    [Fact]
    public void AddChargingSchedule_PartialEnergy_WithGap_ShiftsValidFromLater()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var tStartExisting = CurrentFakeDate;
        var tEndExisting = CurrentFakeDate.AddHours(1);
        var tStartNew = tEndExisting.AddMinutes(10);
        var tEndNew = tStartNew.AddHours(1);

        var existing = CreateSchedule(tStartExisting, tEndExisting, MaxPower);
        var existingSchedules = new List<DtoChargingSchedule> { existing };
        var newSchedule = CreateSchedule(tStartNew, tEndNew, MaxPower);
        var maxEnergyToAdd = MaxPower / 2;

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd);

        Assert.Equal(2, schedules.Count);
        var s1 = schedules.Single(s => s.ValidFrom == tStartExisting);
        Assert.Equal(tEndExisting, s1.ValidTo);

        var s2 = schedules.Single(s => s.ValidTo == tEndNew);
        Assert.Equal(tStartNew.AddMinutes(30), s2.ValidFrom);
        Assert.Equal(maxEnergyToAdd, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialEnergy_Contiguous_ShiftsValidToEarlier()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = CurrentFakeDate.AddHours(1);
        var t3 = CurrentFakeDate.AddHours(2);

        var existing = CreateSchedule(t1, t2, MaxPower);
        var existingSchedules = new List<DtoChargingSchedule> { existing };
        var newSchedule = CreateSchedule(t2, t3, MaxPower);
        var maxEnergyToAdd = MaxPower / 2;

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd);

        Assert.Equal(2, schedules.Count);
        var s1 = schedules.Single(s => s.ValidFrom == t1);
        Assert.Equal(t2, s1.ValidTo);

        var s2 = schedules.Single(s => s.ValidFrom == t2);
        Assert.Equal(t2.AddMinutes(30), s2.ValidTo);
        Assert.Equal(maxEnergyToAdd, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialOverlap_WithPriorNeighbor_MaintainsContinuity()
    {
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = CurrentFakeDate.AddHours(1);
        var t2_5 = CurrentFakeDate.AddMinutes(90);
        var t3 = CurrentFakeDate.AddHours(2);
        var t4 = CurrentFakeDate.AddHours(3);

        var existing1 = CreateSchedule(t1, t2, MaxPower / 2);
        var existing2 = CreateSchedule(t2, t3, MaxPower / 2);
        var existingSchedules = new List<DtoChargingSchedule> { existing1, existing2 };

        var newSchedule = CreateSchedule(t2_5, t4, MaxPower);
        var maxEnergyToAdd = MaxPower;

        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd);

        Assert.Equal(4, schedules.Count);

        var s1 = schedules.Single(s => s.ValidFrom == t1);
        Assert.Equal(t2, s1.ValidTo);
        Assert.Equal(MaxPower / 2, s1.TargetMinPower);

        var s2 = schedules.Single(s => s.ValidFrom == t2);
        Assert.Equal(t2_5, s2.ValidTo);
        Assert.Equal(MaxPower / 2, s2.TargetMinPower);

        var s3 = schedules.Single(s => s.ValidFrom == t2_5);
        Assert.Equal(t3, s3.ValidTo);
        Assert.Equal(MaxPower, s3.TargetMinPower);

        var s4 = schedules.Single(s => s.ValidFrom == t3);
        Assert.Equal(t3.AddMinutes(45), s4.ValidTo);
        Assert.Equal(MaxPower, s4.TargetMinPower);

        Assert.Equal(maxEnergyToAdd, addedEnergy);
    }
}
