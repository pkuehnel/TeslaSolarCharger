using System;
using System.Collections.Generic;
using TeslaSolarCharger.Shared.Dtos;
using Xunit;
using Xunit.Abstractions;
using TeslaSolarCharger.Tests;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class ShouldScheduleFromStartInsteadOfUntilEndTests : TestBase
{
    private readonly ITestOutputHelper _outputHelper;

    public ShouldScheduleFromStartInsteadOfUntilEndTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void ShouldScheduleFromStartInsteadOfUntilEnd_ReturnsExpectedResult(
        List<DtoChargingSchedule> existingSchedules,
        DtoChargingSchedule newSchedule,
        bool expectedResult,
        string testDescription)
    {
        // Log the test description for clarity in output
        _outputHelper.WriteLine($"Running test: {testDescription}");

        // Arrange
        // Create the service with mocked dependencies
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Act
        var result = service.ShouldScheduleFromStartInsteadOfUntilEnd(existingSchedules, newSchedule);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    public static TheoryData<List<DtoChargingSchedule>, DtoChargingSchedule, bool, string> TestData()
    {
        var data = new TheoryData<List<DtoChargingSchedule>, DtoChargingSchedule, bool, string>();
        var baseTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // Helper to create schedule
        DtoChargingSchedule CreateSchedule(DateTimeOffset start, DateTimeOffset end, int targetMinPower, int? targetHomeBatteryPower)
        {
             var s = new DtoChargingSchedule(1, 1, 10000, new HashSet<ScheduleReason>())
             {
                 ValidFrom = start,
                 ValidTo = end,
                 TargetMinPower = targetMinPower,
                 TargetHomeBatteryPower = targetHomeBatteryPower
             };
             return s;
        }

        // 1. Empty existing schedules list.
        // Expect False because there are no schedules to be contiguous with.
        data.Add(
            new List<DtoChargingSchedule>(),
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            false,
            "Empty existing schedules list"
        );

        // 2. Existing schedule ends before new schedule starts (Gap).
        // Expect False because ValidTo of existing != ValidFrom of new.
        data.Add(
            new List<DtoChargingSchedule> { CreateSchedule(baseTime.AddHours(-2), baseTime.AddHours(-1), 1000, 0) },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            false,
            "Existing ends before new starts (Gap)"
        );

        // 3. Existing schedule ends exactly when new schedule starts (Contiguous).

        // 3a. Existing has TargetMinPower > 0.
        // Expect True.
        data.Add(
            new List<DtoChargingSchedule> { CreateSchedule(baseTime.AddHours(-1), baseTime, 1000, 0) },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            true,
            "Contiguous, Existing TargetMinPower > 0"
        );

        // 3b. Existing has TargetHomeBatteryPower > 0 (and TargetMinPower == 0).
        // Expect True.
        data.Add(
            new List<DtoChargingSchedule> { CreateSchedule(baseTime.AddHours(-1), baseTime, 0, 1000) },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            true,
            "Contiguous, Existing TargetHomeBatteryPower > 0"
        );

        // 3c. Existing has both > 0.
        // Expect True.
        data.Add(
            new List<DtoChargingSchedule> { CreateSchedule(baseTime.AddHours(-1), baseTime, 1000, 1000) },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            true,
            "Contiguous, Both Existing Powers > 0"
        );

        // 3d. Existing has both 0 (and TargetHomeBatteryPower can be 0).
        // Expect False because it's not considered an 'active' charging block to extend from.
        data.Add(
            new List<DtoChargingSchedule> { CreateSchedule(baseTime.AddHours(-1), baseTime, 0, 0) },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            false,
            "Contiguous, Both Existing Powers 0"
        );

        // 3e. Existing has both 0/null.
        // Expect False.
        data.Add(
            new List<DtoChargingSchedule> { CreateSchedule(baseTime.AddHours(-1), baseTime, 0, null) },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            false,
            "Contiguous, TargetMinPower 0 and HomeBatteryPower Null"
        );

        // 4. Multiple existing schedules.
        // 4a. The relevant schedule (contiguous one) is active.
        // Expect True.
        data.Add(
            new List<DtoChargingSchedule> {
                CreateSchedule(baseTime.AddHours(-2), baseTime.AddHours(-1), 0, 0),
                CreateSchedule(baseTime.AddHours(-1), baseTime, 1000, 0)
            },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            true,
            "Multiple existing, last one contiguous and active"
        );

        // 4b. The relevant schedule is inactive (but contiguous).
        // Expect False.
        data.Add(
            new List<DtoChargingSchedule> {
                CreateSchedule(baseTime.AddHours(-2), baseTime.AddHours(-1), 1000, 0),
                CreateSchedule(baseTime.AddHours(-1), baseTime, 0, 0)
            },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            false,
            "Multiple existing, last one contiguous but inactive"
        );

         // 5. Existing schedule overlaps (ValidTo > ValidFrom).
         // The method checks `s.ValidTo == dtoChargingSchedule.ValidFrom`.
         // Strictly speaking, if they overlap, ValidTo > ValidFrom, so equality is false (assuming no overlap logic is handled inside the method itself, which is true).
         // Expect False.
         data.Add(
            new List<DtoChargingSchedule> { CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0) },
            CreateSchedule(baseTime, baseTime.AddHours(1), 1000, 0),
            false,
            "Overlapping (ValidTo > ValidFrom) - Not strictly contiguous start-to-end"
        );

        return data;
    }
}
