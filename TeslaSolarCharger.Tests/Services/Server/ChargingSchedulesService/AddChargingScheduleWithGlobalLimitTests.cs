using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Extras.Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class AddChargingScheduleWithGlobalLimitTests : TestBase
{
    private const int MaxPower = 11_040;
    private const int MaxCurrent = 16;
    private const int Phases = 3;
    private const int Voltage = 230;

    public AddChargingScheduleWithGlobalLimitTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private DtoChargingSchedule CreateSchedule(DateTimeOffset from, DateTimeOffset to, int power, int maxPower = MaxPower, int phases = Phases, int voltage = Voltage)
    {
        return new DtoChargingSchedule(1, null, maxPower, voltage, phases, new() { ScheduleReason.LatestPossibleTime })
        {
            ValidFrom = from,
            ValidTo = to,
            TargetMinPower = power,
            EstimatedSolarPower = power,
            MaxPossiblePower = maxPower
        };
    }

    [Fact]
    public void AddChargingSchedule_WithOtherLoadPointsUsingPartialCurrent_CapsPowerCorrectly()
    {
        // Arrange
        var service = Mock.Create<ChargingScheduleService>();
        var configMock = Mock.Mock<IConfigurationWrapper>();

        // Global Limit: 20 Amps
        configMock.Setup(x => x.MaxCombinedCurrent()).Returns(20);

        var start = CurrentFakeDate;
        var end = CurrentFakeDate.AddHours(1);

        // Other Load Point: Using 10A on 3 Phases (Current = 10A)
        // Power = 10 * 230 * 3 = 6900
        var otherSchedule = CreateSchedule(start, end, 6900, 11040, 3, 230);
        var otherLoadPointsSchedules = new List<DtoChargingSchedule> { otherSchedule };

        // Existing Schedules for THIS car (Empty)
        var existingSchedules = new List<DtoChargingSchedule>();

        // New Schedule: Wants full 16A (11kW)
        // Expected Available: 20A - 10A = 10A
        // Expected Power: 10 * 230 * 3 = 6900
        var newSchedule = CreateSchedule(start, end, MaxPower, MaxPower, 3, 230);

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(
            existingSchedules, newSchedule, MaxPower, MaxPower, otherLoadPointsSchedules);
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        Assert.Single(schedules);
        var result = schedules.First();

        // MaxPossiblePower should be capped at 6900 (10A)
        Assert.Equal(6900, result.MaxPossiblePower);
        Assert.Equal(6900, result.TargetMinPower);

        // Energy should be 6900Wh (1 hour * 6900W)
        Assert.Equal(6900, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_WithOtherLoadPointsUsingFullCurrent_AddsNoPower()
    {
        // Arrange
        var service = Mock.Create<ChargingScheduleService>();
        var configMock = Mock.Mock<IConfigurationWrapper>();

        // Global Limit: 16 Amps
        configMock.Setup(x => x.MaxCombinedCurrent()).Returns(16);

        var start = CurrentFakeDate;
        var end = CurrentFakeDate.AddHours(1);

        // Other Load Point: Using 16A (Full Capacity)
        var otherSchedule = CreateSchedule(start, end, 11040, 11040, 3, 230);
        var otherLoadPointsSchedules = new List<DtoChargingSchedule> { otherSchedule };

        var existingSchedules = new List<DtoChargingSchedule>();
        var newSchedule = CreateSchedule(start, end, MaxPower, MaxPower, 3, 230);

        // Act
        var (schedules, addedEnergy) = service.AddChargingSchedule(
            existingSchedules, newSchedule, MaxPower, MaxPower, otherLoadPointsSchedules);
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        // Should return empty list because MaxPossiblePower <= 0 is skipped?
        // Logic says "if (dtoChargingSchedule.MaxPossiblePower <= 0) continue"
        Assert.Empty(schedules);
        Assert.Equal(0, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_WithSinglePhaseConstraint_CapsThreePhaseCar()
    {
        // Arrange
        var service = Mock.Create<ChargingScheduleService>();
        var configMock = Mock.Mock<IConfigurationWrapper>();

        // Global Limit: 20 Amps
        configMock.Setup(x => x.MaxCombinedCurrent()).Returns(20);

        var start = CurrentFakeDate;
        var end = CurrentFakeDate.AddHours(1);

        // Other Load Point: Single Phase, 16A
        // Power = 16 * 230 * 1 = 3680
        var otherSchedule = CreateSchedule(start, end, 3680, 3680, 1, 230);
        var otherLoadPointsSchedules = new List<DtoChargingSchedule> { otherSchedule };

        // New Schedule: 3 Phase, wants 16A (11kW)
        // Available Current on L1 = 20 - 16 = 4A
        // Available Current on L2 = 20 - 0 = 20A
        // Available Current on L3 = 20 - 0 = 20A
        // Constraint for 3-phase charging is min(L1, L2, L3) = 4A
        // Allowed Power = 4 * 230 * 3 = 2760
        var newSchedule = CreateSchedule(start, end, 11040, 11040, 3, 230);

        // Act
        var existingSchedules = new List<DtoChargingSchedule>();
        var (schedules, addedEnergy) = service.AddChargingSchedule(
            existingSchedules, newSchedule, MaxPower, MaxPower, otherLoadPointsSchedules);
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        Assert.Single(schedules);
        var result = schedules.First();

        Assert.Equal(2760, result.MaxPossiblePower);
        Assert.Equal(2760, result.TargetMinPower);
        Assert.Equal(2760, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_SplitsScheduleCorrectly_WhenOtherCarStartsChargingMidway()
    {
        // Arrange
        var service = Mock.Create<ChargingScheduleService>();
        var configMock = Mock.Mock<IConfigurationWrapper>();

        // Global Limit: 20 Amps
        configMock.Setup(x => x.MaxCombinedCurrent()).Returns(20);

        var t0 = CurrentFakeDate;
        var t1 = CurrentFakeDate.AddHours(1);
        var t2 = CurrentFakeDate.AddHours(2);

        // Other Load Point: Starts at t1, runs to t2. Using 10A (3-phase) -> 6900W
        var otherSchedule = CreateSchedule(t1, t2, 6900, 11040, 3, 230);
        var otherLoadPointsSchedules = new List<DtoChargingSchedule> { otherSchedule };

        // New Schedule: t0 to t2. Wants 16A (11040W)
        var newSchedule = CreateSchedule(t0, t2, 11040, 11040, 3, 230);

        // Act
        var existingSchedules = new List<DtoChargingSchedule>();
        var (schedules, addedEnergy) = service.AddChargingSchedule(
            existingSchedules, newSchedule, MaxPower, MaxPower * 10, otherLoadPointsSchedules);
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        // Should have 2 segments
        Assert.Equal(2, schedules.Count);

        // Segment 1: t0 -> t1. No competition. Full Power (16A -> 11040W)
        var seg1 = schedules.Single(s => s.ValidFrom == t0);
        Assert.Equal(t1, seg1.ValidTo);
        Assert.Equal(11040, seg1.MaxPossiblePower);

        // Segment 2: t1 -> t2. Competition (10A used). Available 10A. Power -> 6900W.
        var seg2 = schedules.Single(s => s.ValidFrom == t1);
        Assert.Equal(t2, seg2.ValidTo);
        Assert.Equal(6900, seg2.MaxPossiblePower);

        // Total Energy
        // 1h @ 11040 + 1h @ 6900 = 17940
        Assert.Equal(17940, addedEnergy);
    }

    [Fact]
    public void AddChargingSchedule_SplitsScheduleCorrectly_WhenOtherCarAlreadyChargingMidway()
    {
        // Arrange
        var service = Mock.Create<ChargingScheduleService>();
        var configMock = Mock.Mock<IConfigurationWrapper>();

        // Global Limit: 28 Amps
        var globalCurrentLimit = 28;
        configMock.Setup(x => x.MaxCombinedCurrent()).Returns(globalCurrentLimit);

        var t0 = CurrentFakeDate;
        var t1 = CurrentFakeDate.AddHours(1);
        var t2 = CurrentFakeDate.AddHours(2);
        var t3 = CurrentFakeDate.AddHours(3);

        // Other Load Point: Starts at t1, runs to t2. Using 10A (3-phase) -> 6900W
        var otherCurrent = 16;
        var otherPhases = 1;
        var otherVoltage = 230;
        var otherSchedulePower = otherCurrent * otherPhases * 230;
        var otherSchedule = CreateSchedule(t1, t2, otherSchedulePower, otherSchedulePower, otherPhases, otherVoltage);
        var otherLoadPointsSchedules = new List<DtoChargingSchedule> { otherSchedule };

        // New Schedule: t0 to t2. Wants 16A (11040W)
        var thisSchedulePower = 11_040;
        var newSchedule = CreateSchedule(t0, t3, thisSchedulePower, thisSchedulePower, 3, 230);

        // Act
        var scheduleEnergy = MaxPower * 2;
        var existingSchedules = new List<DtoChargingSchedule>();
        var (schedules, addedEnergy) = service.AddChargingSchedule(
            existingSchedules, newSchedule, MaxPower, scheduleEnergy, otherLoadPointsSchedules);
        var expectedDifference = schedules.Sum(s => s.EstimatedEnergy) - existingSchedules.Sum(s => s.EstimatedEnergy);
        Assert.InRange(addedEnergy, expectedDifference - schedules.Count, expectedDifference + schedules.Count);
        // Assert
        // Should have 2 segments
        Assert.Equal(3, schedules.Count);

        // Segment 1: t0 -> t1. No competition. Full Power (16A -> 11040W)
        var seg1 = schedules.Single(s => s.ValidTo == t1);
        Assert.Equal(t1, seg1.ValidTo);
        Assert.Equal(t1.AddMinutes(-15), seg1.ValidFrom);
        Assert.Equal(11040, seg1.MaxPossiblePower);

        // Segment 2: t1 -> t2. Competition (10A used). Available 10A. Power -> 6900W.
        var expectedRestPower = (globalCurrentLimit - otherCurrent) * 3 * 230;
        var seg2 = schedules.Single(s => s.ValidFrom == t1);
        Assert.Equal(t2, seg2.ValidTo);
        Assert.Equal(expectedRestPower, seg2.MaxPossiblePower);

        var seg3 = schedules.Single(s => s.ValidFrom == t2);
        Assert.Equal(t3, seg3.ValidTo);
        Assert.Equal(11040, seg3.MaxPossiblePower);

        // Total Energy
        // 1h @ 11040 + 1h @ 6900 = 17940
        Assert.Equal(scheduleEnergy, addedEnergy);
    }
}
