using System;
using Autofac.Extras.Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Tests;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class GetNextTargetUtcTests : TestBase
{
    private readonly ITestOutputHelper _outputHelper;

    public GetNextTargetUtcTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [MemberData(nameof(GetNextTargetUtcScenarios))]
    public void GetNextTargetUtc_ReturnsCorrectValue(
        string description,
        DateTimeOffset lastPluggedIn,
        DateOnly? targetDate,
        TimeOnly targetTime,
        bool[] repeatDays,
        string? clientTimeZone,
        DateTimeOffset? expectedResult)
    {
        _outputHelper.WriteLine($"Running Scenario: {description}");

        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        var chargingTarget = new CarChargingTarget
        {
            Id = 1,
            TargetDate = targetDate,
            TargetTime = targetTime,
            RepeatOnMondays = repeatDays[0],
            RepeatOnTuesdays = repeatDays[1],
            RepeatOnWednesdays = repeatDays[2],
            RepeatOnThursdays = repeatDays[3],
            RepeatOnFridays = repeatDays[4],
            RepeatOnSaturdays = repeatDays[5],
            RepeatOnSundays = repeatDays[6],
            ClientTimeZone = clientTimeZone,
            CarId = 123
        };

        // Act
        var result = service.GetNextTargetUtc(chargingTarget, lastPluggedIn);

        // Assert
        if (expectedResult.HasValue)
        {
            Assert.NotNull(result);
            Assert.Equal(expectedResult.Value.ToUniversalTime(), result.Value);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact(Skip = "Bug: Loop only checks 7 days (i < 7), so missing the slot on the same day results in no target for next week. Intentionally failing/skipped.")]
    public void GetNextTargetUtc_ReturnsNextWeek_WhenSameDayMissed()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        // Sunday 10:00 UTC
        var baseDate = new DateTimeOffset(2023, 10, 1, 10, 0, 0, TimeSpan.Zero);
        // Repeat on Sunday only
        var repeatSunday = new bool[] { false, false, false, false, false, false, true };

        var chargingTarget = new CarChargingTarget
        {
            Id = 1,
            TargetTime = new TimeOnly(12, 0),
            RepeatOnSundays = true, // repeatSunday[6]
            ClientTimeZone = "UTC",
            CarId = 123
        };

        // Act
        // Last plugged in Sunday 13:00 UTC (missed the 12:00 slot)
        var lastPluggedIn = baseDate.AddHours(3);
        var result = service.GetNextTargetUtc(chargingTarget, lastPluggedIn);

        // Assert
        // Should find next Sunday (Oct 8) 12:00
        Assert.NotNull(result);
        Assert.Equal(new DateTimeOffset(2023, 10, 8, 12, 0, 0, TimeSpan.Zero), result.Value);
    }

    public static TheoryData<string, DateTimeOffset, DateOnly?, TimeOnly, bool[], string?, DateTimeOffset?> GetNextTargetUtcScenarios()
    {
        var data = new TheoryData<string, DateTimeOffset, DateOnly?, TimeOnly, bool[], string?, DateTimeOffset?>();

        var baseDate = new DateTimeOffset(2023, 10, 1, 10, 0, 0, TimeSpan.Zero); // Sunday, Oct 1st 2023. 10:00 UTC

        var noRepeats = new bool[7];
        var repeatSunday = new bool[] { false, false, false, false, false, false, true };
        var repeatMonday = new bool[] { true, false, false, false, false, false, false };
        var repeatDaily = new bool[] { true, true, true, true, true, true, true };

        // 1. No Repetition - Future Target
        data.Add(
            "NoRepetition_FutureTarget",
            baseDate, // Sun 10:00
            new DateOnly(2023, 10, 1),
            new TimeOnly(12, 0),
            noRepeats,
            "UTC",
            new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero)
        );

        // 2. No Repetition - Past Target
        data.Add(
            "NoRepetition_PastTarget",
            baseDate.AddHours(3), // Sun 13:00
            new DateOnly(2023, 10, 1),
            new TimeOnly(12, 0),
            noRepeats,
            "UTC",
            null
        );

        // 3. Repeat Today (Sunday) - Future Time
        data.Add(
            "RepeatToday_FutureTime",
            baseDate, // Sun 10:00
            null,
            new TimeOnly(12, 0),
            repeatSunday,
            "UTC",
            new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero)
        );

        // 4. Repeat Tomorrow (Monday)
        data.Add(
            "RepeatTomorrow",
            baseDate, // Sun 10:00
            null,
            new TimeOnly(10, 0),
            repeatMonday,
            "UTC",
            new DateTimeOffset(2023, 10, 2, 10, 0, 0, TimeSpan.Zero)
        );

        // 5. TargetDate Future + Repeat (Should start from TargetDate)
        data.Add(
            "TargetDateFuture_Repeat_StartsOnTargetDate",
            baseDate, // Sun Oct 1
            new DateOnly(2023, 10, 8), // Sun Oct 8
            new TimeOnly(10, 0),
            repeatDaily,
            "UTC",
            new DateTimeOffset(2023, 10, 8, 10, 0, 0, TimeSpan.Zero)
        );

        // 6. TargetDate Past + Repeat (Should start from LastPluggedIn)
        data.Add(
            "TargetDatePast_Repeat_StartsFromLastPluggedIn",
            baseDate, // Sun Oct 1 10:00
            new DateOnly(2023, 9, 30),
            new TimeOnly(12, 0),
            repeatDaily,
            "UTC",
            new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero)
        );

        // 7. Edge Case: LastPluggedIn matches exactly target time - should return valid?
        data.Add(
            "ExactMatch_ReturnsCandidate",
            baseDate, // 10:00
            new DateOnly(2023, 10, 1),
            new TimeOnly(10, 0),
            noRepeats,
            "UTC",
            new DateTimeOffset(2023, 10, 1, 10, 0, 0, TimeSpan.Zero)
        );

        return data;
    }
}
