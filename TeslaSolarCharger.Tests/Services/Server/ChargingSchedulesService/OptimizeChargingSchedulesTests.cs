using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Extras.Moq;
using TeslaSolarCharger.Shared.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class OptimizeChargingSchedulesTests : TestBase
{
    private const int MaxPower = 11_040;
    private const int DefaultMinChargingPower = 1380;

    public OptimizeChargingSchedulesTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    private DtoChargingSchedule CreateSchedule(
        DateTimeOffset from,
        DateTimeOffset to,
        int targetMinPower,
        int maxPossiblePower = MaxPower,
        int? targetHomeBatteryPower = null,
        int estimatedSolarPower = 0,
        int? carId = 1,
        int? ocppConnectorId = null,
        ScheduleReason reason = ScheduleReason.LatestPossibleTime)
    {
        return new DtoChargingSchedule(carId, ocppConnectorId, maxPossiblePower, new() { reason })
        {
            ValidFrom = from,
            ValidTo = to,
            TargetMinPower = targetMinPower,
            TargetHomeBatteryPower = targetHomeBatteryPower,
            EstimatedSolarPower = estimatedSolarPower
        };
    }

    [Fact]
    public void OptimizeChargingSchedules_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var schedules = new List<DtoChargingSchedule>();

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void OptimizeChargingSchedules_LeadingGap_CurrentlyCharging_SmallGap_FillsGap()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Gap is 15 mins (<= 20 mins)
        var gapMinutes = 15;
        var start = CurrentFakeDate.AddMinutes(gapMinutes);
        var end = start.AddHours(1);

        var schedule = CreateSchedule(start, end, MaxPower);
        var schedules = new List<DtoChargingSchedule> { schedule };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, true, DefaultMinChargingPower);

        // Assert
        Assert.Equal(2, result.Count);

        var filler = result[0];
        Assert.Equal(CurrentFakeDate, filler.ValidFrom);
        Assert.Equal(start, filler.ValidTo);
        Assert.Equal(DefaultMinChargingPower, filler.TargetMinPower);
        Assert.Contains(ScheduleReason.BridgeSchedules, filler.ScheduleReasons);

        var original = result[1];
        Assert.Equal(start, original.ValidFrom);
    }

    [Fact]
    public void OptimizeChargingSchedules_LeadingGap_CurrentlyCharging_LargeGap_NoFill()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Gap is 21 mins (> 20 mins)
        var gapMinutes = 21;
        var start = CurrentFakeDate.AddMinutes(gapMinutes);
        var end = start.AddHours(1);

        var schedule = CreateSchedule(start, end, MaxPower);
        var schedules = new List<DtoChargingSchedule> { schedule };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, true, DefaultMinChargingPower);

        // Assert
        Assert.Single(result);
        Assert.Equal(start, result[0].ValidFrom);
    }

    [Fact]
    public void OptimizeChargingSchedules_LeadingGap_NotCharging_NoFill()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Gap is 15 mins (would fill if charging)
        var gapMinutes = 15;
        var start = CurrentFakeDate.AddMinutes(gapMinutes);
        var end = start.AddHours(1);

        var schedule = CreateSchedule(start, end, MaxPower);
        var schedules = new List<DtoChargingSchedule> { schedule };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Single(result);
        Assert.Equal(start, result[0].ValidFrom);
    }

    [Fact]
    public void OptimizeChargingSchedules_InternalGap_SmallGap_FillsGap()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var start1 = CurrentFakeDate;
        var end1 = CurrentFakeDate.AddHours(1);

        // Gap 15 mins
        var start2 = end1.AddMinutes(15);
        var end2 = start2.AddHours(1);

        var s1 = CreateSchedule(start1, end1, MaxPower);
        var s2 = CreateSchedule(start2, end2, MaxPower);
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Equal(3, result.Count);

        Assert.Equal(s1.ValidFrom, result[0].ValidFrom);

        var filler = result[1];
        Assert.Equal(end1, filler.ValidFrom);
        Assert.Equal(start2, filler.ValidTo);
        Assert.Equal(DefaultMinChargingPower, filler.TargetMinPower);
        Assert.Contains(ScheduleReason.BridgeSchedules, filler.ScheduleReasons);

        Assert.Equal(s2.ValidFrom, result[2].ValidFrom);
    }

    [Fact]
    public void OptimizeChargingSchedules_InternalGap_LargeGap_NoFill()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var start1 = CurrentFakeDate;
        var end1 = CurrentFakeDate.AddHours(1);

        // Gap 21 mins
        var start2 = end1.AddMinutes(21);
        var end2 = start2.AddHours(1);

        var s1 = CreateSchedule(start1, end1, MaxPower);
        var s2 = CreateSchedule(start2, end2, MaxPower);
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(end1, result[0].ValidTo);
        Assert.Equal(start2, result[1].ValidFrom);
    }

    [Fact]
    public void OptimizeChargingSchedules_Contiguous_IdenticalProps_Merges()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var start1 = CurrentFakeDate;
        var end1 = CurrentFakeDate.AddHours(1);

        // Contiguous
        var start2 = end1;
        var end2 = start2.AddHours(1);

        // Identical properties
        var s1 = CreateSchedule(start1, end1, MaxPower);
        var s2 = CreateSchedule(start2, end2, MaxPower);
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Single(result);
        Assert.Equal(start1, result[0].ValidFrom);
        Assert.Equal(end2, result[0].ValidTo);
        Assert.Equal(MaxPower, result[0].TargetMinPower);
    }

    [Fact]
    public void OptimizeChargingSchedules_Contiguous_DifferentTargetMinPower_NoMerge()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = t1.AddHours(1);
        var t3 = t2.AddHours(1);

        var s1 = CreateSchedule(t1, t2, MaxPower);
        var s2 = CreateSchedule(t2, t3, MaxPower - 100); // Different
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void OptimizeChargingSchedules_Contiguous_DifferentTargetHomeBatteryPower_NoMerge()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = t1.AddHours(1);
        var t3 = t2.AddHours(1);

        var s1 = CreateSchedule(t1, t2, MaxPower, targetHomeBatteryPower: 1000);
        var s2 = CreateSchedule(t2, t3, MaxPower, targetHomeBatteryPower: 2000); // Different
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void OptimizeChargingSchedules_Contiguous_DifferentEstimatedSolarPower_NoMerge()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = t1.AddHours(1);
        var t3 = t2.AddHours(1);

        var s1 = CreateSchedule(t1, t2, MaxPower, estimatedSolarPower: 1000);
        var s2 = CreateSchedule(t2, t3, MaxPower, estimatedSolarPower: 2000); // Different
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void OptimizeChargingSchedules_Contiguous_DifferentMaxPossiblePower_NoMerge()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = t1.AddHours(1);
        var t3 = t2.AddHours(1);

        var s1 = CreateSchedule(t1, t2, MaxPower, maxPossiblePower: MaxPower);
        var s2 = CreateSchedule(t2, t3, MaxPower, maxPossiblePower: MaxPower - 100); // Different
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void OptimizeChargingSchedules_Contiguous_DifferentCarId_NoMerge()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = t1.AddHours(1);
        var t3 = t2.AddHours(1);

        var s1 = CreateSchedule(t1, t2, MaxPower, carId: 1);
        var s2 = CreateSchedule(t2, t3, MaxPower, carId: 2); // Different
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void OptimizeChargingSchedules_Contiguous_DifferentOcppConnectorId_NoMerge()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var t1 = CurrentFakeDate;
        var t2 = t1.AddHours(1);
        var t3 = t2.AddHours(1);

        var s1 = CreateSchedule(t1, t2, MaxPower, ocppConnectorId: 1);
        var s2 = CreateSchedule(t2, t3, MaxPower, ocppConnectorId: 2); // Different
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void OptimizeChargingSchedules_GapAndMerge_ComplexScenario()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Scenario:
        // Schedule 1: 10:00 - 10:45
        // Gap: 10:45 - 11:00 (15 min) -> Should be filled
        // Schedule 2: 11:00 - 12:00
        //
        // If Schedule 1, Filled Gap, and Schedule 2 have same properties (including target power),
        // they should all merge into one large schedule.

        var t1 = CurrentFakeDate;
        var t2 = t1.AddMinutes(45);
        var t3 = t1.AddHours(1); // 11:00
        var t4 = t1.AddHours(2); // 12:00

        // Set all to DefaultMinChargingPower so they match the filler
        var s1 = CreateSchedule(t1, t2, DefaultMinChargingPower);
        var s2 = CreateSchedule(t3, t4, DefaultMinChargingPower);
        var schedules = new List<DtoChargingSchedule> { s1, s2 };

        // Act
        var result = service.OptimizeChargingSchedules(schedules, CurrentFakeDate, false, DefaultMinChargingPower);

        // Assert
        // 1. Gap is filled (10:45 - 11:00).
        // 2. Filled gap has DefaultMinChargingPower.
        // 3. s1 and s2 also have DefaultMinChargingPower.
        // 4. They are contiguous.
        // Result: 1 big schedule from 10:00 to 12:00.

        Assert.Single(result);
        Assert.Equal(t1, result[0].ValidFrom);
        Assert.Equal(t4, result[0].ValidTo);
        Assert.Equal(DefaultMinChargingPower, result[0].TargetMinPower);
    }
}
