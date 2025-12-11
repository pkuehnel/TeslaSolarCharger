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
    private const int HomeBatPower = 4_500;

    private const int Phases = 3;
    private const int Voltage = 230;

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
        return new DtoChargingSchedule(1, null, maxPower, Voltage, Phases, new() { reason })
        {
            ValidFrom = from,
            ValidTo = to,
            TargetMinPower = targetPower,
            MaxPossiblePower = maxPower,
        };
    }

    public enum PreviousScheduleType
    {
        ExistingMinPower,
        ExistingMinHomeBatteryPower,
        ExistingExpectedSolarPower,
        ExistingNoPower,
        NoExistingSchedule,
    }

    [Theory]
    //previous schedule has min power
    [InlineData(PreviousScheduleType.ExistingMinPower, "StartHigh")]
    //previous schedule has min home battery power
    [InlineData(PreviousScheduleType.ExistingMinHomeBatteryPower, "StartHigh")]
    //previous schedule has expected solar power (should be ignored on scheduling following schedules as not sure if power is really there)
    [InlineData(PreviousScheduleType.ExistingExpectedSolarPower, "EndHigh")]
    //previous schedule has no
    [InlineData(PreviousScheduleType.ExistingNoPower, "EndHigh")]
    //there is no previous schedule
    [InlineData(PreviousScheduleType.NoExistingSchedule, "EndHigh")]
    public void AddChargingSchedule_PartialOverlapEnergy_ShouldSplitScheduleCorrectly(PreviousScheduleType previousScheduleType, string expectedHighPowerPosition)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var start = CurrentFakeDate;
        var end = CurrentFakeDate.AddHours(1);

        // Create existing schedule list
        var existingSchedules = new List<DtoChargingSchedule>();

        // If contiguous, we add a dummy schedule BEFORE the target slot
        if (previousScheduleType == 0)
        {
            
        }

        DtoChargingSchedule? dtoChargingSchedule = null;
        switch (previousScheduleType)
        {
            case PreviousScheduleType.ExistingMinPower:
                dtoChargingSchedule = CreateSchedule(start.AddMinutes(-10), start, 1500, MaxPower, ScheduleReason.LatestPossibleTime);
                break;
            case PreviousScheduleType.ExistingMinHomeBatteryPower:
                dtoChargingSchedule = CreateSchedule(start.AddMinutes(-10), start, 0, MaxPower, ScheduleReason.LatestPossibleTime);
                dtoChargingSchedule.TargetHomeBatteryPower = 5000;
                break;
            case PreviousScheduleType.ExistingExpectedSolarPower:
                dtoChargingSchedule = CreateSchedule(start.AddMinutes(-10), start, 0, MaxPower, ScheduleReason.LatestPossibleTime);
                dtoChargingSchedule.EstimatedSolarPower = 5000;
                break;
            case PreviousScheduleType.ExistingNoPower:
                dtoChargingSchedule = CreateSchedule(start.AddMinutes(-10), start, 0, MaxPower, ScheduleReason.LatestPossibleTime);
                break;
            case PreviousScheduleType.NoExistingSchedule:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(previousScheduleType), previousScheduleType, null);
        }

        if (dtoChargingSchedule != default)
        {
            existingSchedules.Add(dtoChargingSchedule);
        }

        // Existing overlapping schedule: HomeBatteryDischarging (4500W)
        var existingSchedule = CreateSchedule(start, end, 0, MaxPower, ScheduleReason.HomeBatteryDischarging);
        existingSchedule.EstimatedSolarPower = HomeBatPower;
        existingSchedules.Add(existingSchedule);

        // New: CheapGridPrice (11040W) overlapping
        // We set TargetMinPower to MaxPower.
        var newSchedule = CreateSchedule(start, end, MaxPower, MaxPower, ScheduleReason.CheapGridPrice);

        // We want to add only 1000Wh of Grid energy.
        // Full grid boost would provide (11040 - 4500) * 1h = 6540Wh.
        // We limit to 1000Wh.
        int maxEnergyToAdd = 1000;
        double expectedDuration = 1000.0 / 6540.0; // 1000Wh / 6540W

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd, new());

        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);

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

    [Fact]
    public void AddChargingSchedule_EmptyList_AddsFullSchedule()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var existingSchedules = new List<DtoChargingSchedule>();
        var start = CurrentFakeDate.AddHours(1);
        var end = CurrentFakeDate.AddHours(2);

        var newSchedule = CreateSchedule(start, end, MaxPower);

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        Assert.Single(schedules);
        Assert.Equal(MaxPower, addedEnergy); // 1 hour @ MaxPower = MaxPower energy
        Assert.Equal(start, schedules[0].ValidFrom);
        Assert.Equal(end, schedules[0].ValidTo);
    }

    [Fact]
    public void AddChargingSchedule_EmptyList_EnergyLimit_AdjustsStartTime()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var existingSchedules = new List<DtoChargingSchedule>();
        var start = CurrentFakeDate.AddMinutes(60);
        var end = CurrentFakeDate.AddMinutes(120);

        // We request 1 hour at MaxPower, but allow only half the energy
        var newSchedule = CreateSchedule(start, end, MaxPower);
        var maxEnergyToAdd = MaxPower / 2;

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        Assert.Single(schedules);
        Assert.Equal(maxEnergyToAdd, addedEnergy);

        // Since energy is half, time should be reduced by half (start time shifts forward)
        // Logic check: (slotEnergy - energyToAdd) / Power = (Max - Max/2)/Max = 0.5 hours to reduce
        Assert.Equal(start.AddMinutes(30), schedules[0].ValidFrom);
        Assert.Equal(end, schedules[0].ValidTo);
    }

    // --- NEW TESTS: FULL OVERLAP SCENARIOS ---

    [Fact]
    public void AddChargingSchedule_FullOverlap_IncreasesExistingPower()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var start = CurrentFakeDate.AddHours(1);
        var end = CurrentFakeDate.AddHours(2);

        // Existing schedule running at HALF power
        var existingSchedule = CreateSchedule(start, end, MaxPower / 2);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };

        // New schedule requesting FULL power
        var newSchedule = CreateSchedule(start, end, MaxPower);

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        Assert.Single(schedules);
        var result = schedules.First();

        // 1. Power should be upgraded to MaxPower (Math.Max logic)
        Assert.Equal(MaxPower, result.TargetMinPower);

        // 2. Added Energy should be the DIFFERENCE (Max - Half)
        Assert.Equal(MaxPower / 2, addedEnergy);

        // 3. Reasons should be merged
        Assert.Equal(start, result.ValidFrom);
        Assert.Equal(end, result.ValidTo);
    }

    [Fact]
    public void AddChargingSchedule_FullOverlap_AlreadyMaxPower_NoChange()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var start = CurrentFakeDate.AddHours(1);
        var end = CurrentFakeDate.AddHours(2);

        // Existing is already at MAX power
        var existingSchedule = CreateSchedule(start, end, MaxPower);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };

        // New schedule also at MAX power
        var newSchedule = CreateSchedule(start, end, MaxPower);

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        Assert.Single(schedules);
        var result = schedules.First();

        Assert.Equal(MaxPower, result.TargetMinPower);
        // Should add 0 energy because we are already maxed out
        Assert.Equal(0, addedEnergy);
    }

    // --- NEW TESTS: PARTIAL OVERLAP SCENARIOS ---

    [Fact]
    public void AddChargingSchedule_PartialOverlap_IncreasesPowerOnOverlap_AndAddsNewSlot()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Time segments: 08:00 -> 09:00 -> 10:00
        var t1 = CurrentFakeDate;
        var t2 = CurrentFakeDate.AddHours(1);
        var t3 = CurrentFakeDate.AddHours(2);

        // Existing: 08:00 - 10:00 @ 50% Power
        // Note: The splitter in your service logic is responsible for breaking this up.
        // Assuming the mocked/real splitter functions correctly, it will break the existing schedule 
        // into 08:00-09:00 and 09:00-10:00 to match the new schedule boundaries.
        var existingSchedule = CreateSchedule(t1, t3, MaxPower / 2);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };

        // New: 09:00 - 10:00 @ 100% Power (Overlaps the second half of existing)
        var newSchedule = CreateSchedule(t2, t3, MaxPower);

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        // Result should have:
        // 1. 08:00-09:00 @ 50% (Untouched existing part)
        // 2. 09:00-10:00 @ 100% (Merged part)

        // Note: Depending on how the Splitter works, this might return 2 items.
        // Item 1: 08:00-09:00 (50%)
        // Item 2: 09:00-10:00 (100%)

        Assert.Equal(2, schedules.Count);

        // Check first segment (Unchanged)
        var segment1 = schedules.FirstOrDefault(s => s.ValidFrom == t1);
        Assert.NotNull(segment1);
        Assert.Equal(MaxPower / 2, segment1.TargetMinPower);

        // Check second segment (Upgraded)
        var segment2 = schedules.FirstOrDefault(s => s.ValidFrom == t2);
        Assert.NotNull(segment2);
        Assert.Equal(MaxPower, segment2.TargetMinPower);

        // Energy added is the difference for 1 hour: (Max - Max/2)
        Assert.Equal(MaxPower / 2, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialOverlap_AlreadyMax_NoPowerChange_ButStructureUpdate()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var t1 = CurrentFakeDate;
        var t2 = CurrentFakeDate.AddHours(1);
        var t3 = CurrentFakeDate.AddHours(2);

        // Existing: 08:00 - 10:00 @ 100% Power
        var existingSchedule = CreateSchedule(t1, t3, MaxPower);
        var existingSchedules = new List<DtoChargingSchedule> { existingSchedule };

        // New: 09:00 - 10:00 @ 100% Power
        var newSchedule = CreateSchedule(t2, t3, MaxPower);

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        // Even though power didn't change, the splitter likely physically split the list into two objects
        // to accommodate the boundary checks.
        Assert.Equal(2, schedules.Count);

        var segment2 = schedules.FirstOrDefault(s => s.ValidFrom == t2);
        Assert.NotNull(segment2);
        Assert.Equal(MaxPower, segment2.TargetMinPower);

        // No energy added
        Assert.Equal(0, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_ComplexOverlap_InsertsNewSlot_AndUpgradesExisting()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // 08:00 -> 09:00 -> 10:00 -> 11:00
        var t10 = CurrentFakeDate;
        var t11 = CurrentFakeDate.AddHours(1);
        var t12 = CurrentFakeDate.AddHours(2);

        // Existing: 08:00 - 09:00 @ 50%
        var existing = CreateSchedule(t10, t11, MaxPower / 2);
        var existingSchedules = new List<DtoChargingSchedule> { existing };

        // New: 08:30 - 10:00 @ 100% (Partial overlap 08:30-09:00, New 09:00-10:00)
        // Note: This relies on the Splitter handling the 08:30 cut.
        var t10_30 = t10.AddMinutes(30);
        var newSchedule = CreateSchedule(t10_30, t12, MaxPower);

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, MaxPower * 10, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        // Expected outcome structure based on Splitter logic + Add logic:
        // 1. 08:00 - 08:30 @ 50% (Remaining existing)
        // 2. 08:30 - 09:00 @ 100% (Merged/Upgraded)
        // 3. 09:00 - 10:00 @ 100% (Brand New)

        Assert.Equal(3, schedules.Count);

        // 1. Untouched part
        var part1 = schedules.Single(s => s.ValidFrom == t10);
        Assert.Equal(t10_30, part1.ValidTo);
        Assert.Equal(MaxPower / 2, part1.TargetMinPower);

        // 2. Overlap part
        var part2 = schedules.Single(s => s.ValidFrom == t10_30 && s.ValidTo == t11);
        Assert.Equal(MaxPower, part2.TargetMinPower);

        // 3. New part
        var part3 = schedules.Single(s => s.ValidFrom == t11);
        Assert.Equal(t12, part3.ValidTo);
        Assert.Equal(MaxPower, part3.TargetMinPower);

        // Energy Check:
        // Overlap upgrade: 0.5h * (100% - 50%) = 0.5h * 50% Power
        // New part: 1.0h * 100% Power
        var overlapEnergy = CalculateEnergy(0.5, MaxPower / 2);
        var newPartEnergy = CalculateEnergy(1.0, MaxPower);
        Assert.Equal(overlapEnergy + newPartEnergy, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialEnergy_WithGap_ShiftsValidFromLater()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var tStartExisting = CurrentFakeDate;           // 08:00
        var tEndExisting = CurrentFakeDate.AddHours(1); // 09:00

        // 10 minute gap
        var tStartNew = tEndExisting.AddMinutes(10);    // 09:10
        var tEndNew = tStartNew.AddHours(1);            // 10:10

        var existing = CreateSchedule(tStartExisting, tEndExisting, MaxPower);
        var existingSchedules = new List<DtoChargingSchedule> { existing };

        var newSchedule = CreateSchedule(tStartNew, tEndNew, MaxPower);

        // Limit energy to 50% (30 mins worth of power)
        var maxEnergyToAdd = MaxPower / 2;

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        Assert.Equal(2, schedules.Count);

        // 1. Existing schedule remains untouched
        var s1 = schedules.Single(s => s.ValidFrom == tStartExisting);
        Assert.Equal(tEndExisting, s1.ValidTo);

        // 2. New schedule:
        // Because of the gap, the code shortens from the START (ValidFrom increases).
        // We requested 1h duration but only gave 0.5h energy. 
        // Start should shift by 30 mins: 09:10 -> 09:40.
        var s2 = schedules.Single(s => s.ValidTo == tEndNew);
        Assert.Equal(tStartNew.AddMinutes(30), s2.ValidFrom);

        Assert.Equal(maxEnergyToAdd, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialEnergy_Contiguous_ShiftsValidToEarlier()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var t1 = CurrentFakeDate;           // 08:00
        var t2 = CurrentFakeDate.AddHours(1); // 09:00 (Boundary)
        var t3 = CurrentFakeDate.AddHours(2); // 10:00

        var existing = CreateSchedule(t1, t2, MaxPower);
        var existingSchedules = new List<DtoChargingSchedule> { existing };

        // New schedule starts exactly when existing ends (09:00)
        var newSchedule = CreateSchedule(t2, t3, MaxPower);

        // Limit energy to 50% (30 mins worth)
        var maxEnergyToAdd = MaxPower / 2;

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        Assert.Equal(2, schedules.Count);

        // 1. Existing schedule untouched
        var s1 = schedules.Single(s => s.ValidFrom == t1);
        Assert.Equal(t2, s1.ValidTo);

        // 2. New schedule:
        // Because it is contiguous (Any found matching ValidTo), code shortens from the END.
        // Start remains 09:00. End shifts from 10:00 -> 09:30.
        var s2 = schedules.Single(s => s.ValidFrom == t2);
        Assert.Equal(t2.AddMinutes(30), s2.ValidTo);

        Assert.Equal(maxEnergyToAdd, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialOverlap_WithPriorNeighbor_MaintainsContinuity()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        // Timeline: 08:00 -> 09:00 -> 09:30 -> 10:00 -> 11:00
        var t1 = CurrentFakeDate;                // 08:00
        var t2 = CurrentFakeDate.AddHours(1);    // 09:00
        var t2_5 = CurrentFakeDate.AddMinutes(90); // 09:30
        var t3 = CurrentFakeDate.AddHours(2);    // 10:00
        var t4 = CurrentFakeDate.AddHours(3);    // 11:00

        // Existing: Two distinct 1-hour blocks at 50% power
        // 1. 08:00 - 09:00
        // 2. 09:00 - 10:00
        var existing1 = CreateSchedule(t1, t2, MaxPower / 2);
        var existing2 = CreateSchedule(t2, t3, MaxPower / 2);
        var existingSchedules = new List<DtoChargingSchedule> { existing1, existing2 };

        // New: 09:30 - 11:00 @ 100% Power
        // (Overlaps the last 30 mins of existing2, plus 60 mins of new time)
        var newSchedule = CreateSchedule(t2_5, t4, MaxPower);

        // Energy Limit: Set to 11040 (MaxPower)
        // 1. Overlap (30 mins): Increases power by 50%. Energy cost = 0.5h * 5520 = 2760.
        // 2. Remaining Budget: 11040 - 2760 = 8280.
        // 3. New Slot (1 hour): Wants 11040. Can only afford 8280. 
        //    8280 / 11040 = 0.75 hours (45 minutes).
        var maxEnergyToAdd = MaxPower;

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        // The splitter breaks the list into:
        // 1. 08:00 - 09:00 (Existing 1 - untouched)
        // 2. 09:00 - 09:30 (Existing 2 part A - untouched split)
        // 3. 09:30 - 10:00 (Existing 2 part B - Overlap/Upgraded)
        // 4. 10:00 - 10:45 (New Schedule - Shortened from 11:00 due to energy limit)
        Assert.Equal(4, schedules.Count);

        // 1. Existing First Hour (08:00 - 09:00)
        var s1 = schedules.Single(s => s.ValidFrom == t1);
        Assert.Equal(t2, s1.ValidTo);
        Assert.Equal(MaxPower / 2, s1.TargetMinPower);

        // 2. Existing Second Hour Part A (09:00 - 09:30)
        // This part is outside the new schedule's start time, so it remains at 50%
        var s2 = schedules.Single(s => s.ValidFrom == t2);
        Assert.Equal(t2_5, s2.ValidTo);
        Assert.Equal(MaxPower / 2, s2.TargetMinPower);

        // 3. Existing Second Hour Part B (09:30 - 10:00) - The Overlap
        // This matches the start of newSchedule. Power is upgraded to Max.
        var s3 = schedules.Single(s => s.ValidFrom == t2_5);
        Assert.Equal(t3, s3.ValidTo);
        Assert.Equal(MaxPower, s3.TargetMinPower);

        // 4. New Schedule Portion (10:00 - 10:45)
        // Starts at 10:00.
        // Continuity Logic: Because there is an existing schedule ending at 10:00 (s3),
        // the system shortens the ValidTo (end) rather than the ValidFrom (start).
        // Duration is 0.75h (45m). End time is 10:45.
        var s4 = schedules.Single(s => s.ValidFrom == t3);
        Assert.Equal(t3.AddMinutes(45), s4.ValidTo);
        Assert.Equal(MaxPower, s4.TargetMinPower);

        // Total Added Energy Check
        Assert.Equal(maxEnergyToAdd, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_PartialOverlap_WithPriorNeighbor_AndEnoughUpgradablePowerTo_MaintainsContinuity()
    {
        // Arrange
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
        var maxEnergyToAdd = MaxPower / 8;

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(existingSchedules, newSchedule, MaxPower, maxEnergyToAdd, new());
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);

        Assert.Equal(4, schedules.Count);

        // 1. Existing First Hour (08:00 - 09:00)
        var s1 = schedules.Single(s => s.ValidFrom == t1);
        Assert.Equal(t2, s1.ValidTo);
        Assert.Equal(MaxPower / 2, s1.TargetMinPower);

        var s2 = schedules.Single(s => s.ValidFrom == t2);
        Assert.Equal(t2_5, s2.ValidTo);
        Assert.Equal(MaxPower / 2, s2.TargetMinPower);

        var s3 = schedules.Single(s => s.ValidFrom == t2_5);
        Assert.Equal(t2_5.AddMinutes(15), s3.ValidTo);
        Assert.Equal(MaxPower, s3.TargetMinPower);

        var s4 = schedules.Single(s => s.ValidTo == t3);
        Assert.Equal(t2_5.AddMinutes(15), s4.ValidFrom);
        Assert.Equal(MaxPower / 2, s4.TargetMinPower);

        // Total Added Energy Check
        Assert.Equal(maxEnergyToAdd, addedEnergy);
    }

    // Helper just for the calculation check in the final test
    private int CalculateEnergy(double hours, int power) => (int)(hours * power);

}
