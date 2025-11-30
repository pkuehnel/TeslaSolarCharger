using System;
using System.Collections.Generic;
using System.Reflection;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class TargetChargingValueCalculationServiceTests : TestBase
{
    public TargetChargingValueCalculationServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private static DtoTimeStampedValue<T> CreateDto<T>(T value, DateTimeOffset? lastChanged)
    {
        // Constructor requires a timestamp and a value.
        var dto = new DtoTimeStampedValue<T>(DateTimeOffset.MinValue, value);

        if (lastChanged.HasValue)
        {
            var prop = typeof(DtoTimeStampedValue<T>).GetProperty(nameof(DtoTimeStampedValue<T>.LastChanged));
            // Use non-public setter if necessary, though SetValue typically handles it if property is found
            // But to be sure for private set:
            prop?.SetValue(dto, lastChanged);
        }
        return dto;
    }

    public static IEnumerable<object[]> GetIsTimeStampedValueRelevantData()
    {
        var currentDate = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var timeSpan = TimeSpan.FromMinutes(10);
        var threshold = currentDate - timeSpan; // 11:50:00

        // 1. LastChanged is null (default)
        // Logic: "If no last changed time is set, we assume it is relevant"
        yield return new object[]
        {
            CreateDto(true, null),
            currentDate,
            timeSpan,
            true, // expectedResult
            null  // expectedRelevantAt
        };

        // 2. LastChanged is older than threshold (Relevant)
        // LastChanged: 11:49 (11 mins ago) -> 11:49 < 11:50 -> True
        yield return new object[]
        {
            CreateDto(true, threshold.AddMinutes(-1)),
            currentDate,
            timeSpan,
            true,
            null
        };

        // 3. LastChanged is exactly threshold (Not Relevant yet)
        // LastChanged: 11:50 -> 11:50 < 11:50 -> False
        // RelevantAt: 11:50 + 10m = 12:00 (CurrentDate)
        yield return new object[]
        {
            CreateDto(true, threshold),
            currentDate,
            timeSpan,
            false,
            threshold.Add(timeSpan) // Should be equal to currentDate
        };

        // 4. LastChanged is newer than threshold (Not Relevant)
        // LastChanged: 11:51 (9 mins ago) -> 11:51 < 11:50 -> False
        // RelevantAt: 11:51 + 10m = 12:01
        yield return new object[]
        {
            CreateDto(true, threshold.AddMinutes(1)),
            currentDate,
            timeSpan,
            false,
            threshold.AddMinutes(1).Add(timeSpan)
        };

        // 5. Zero TimeSpan
        // Threshold = CurrentDate.
        // LastChanged = CurrentDate (just happened).
        // Current < Current -> False.
        // RelevantAt = Current + 0 = Current.
        yield return new object[]
        {
            CreateDto(true, currentDate),
            currentDate,
            TimeSpan.Zero,
            false,
            currentDate
        };

        // 6. Zero TimeSpan, LastChanged in Past
        // LastChanged < Current -> True.
        yield return new object[]
        {
            CreateDto(true, currentDate.AddSeconds(-1)),
            currentDate,
            TimeSpan.Zero,
            true,
            null
        };
    }

    [Theory]
    [MemberData(nameof(GetIsTimeStampedValueRelevantData))]
    public void IsTimeStampedValueRelevant_CalculatesCorrectly(
        DtoTimeStampedValue<bool> input,
        DateTimeOffset currentDate,
        TimeSpan timeSpan,
        bool expectedResult,
        DateTimeOffset? expectedRelevantAt)
    {
        // Arrange
        // Use fully qualified name to avoid namespace collisions if any
        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // Act
        var result = service.IsTimeStampedValueRelevant(input, currentDate, timeSpan, out var relevantAt);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedRelevantAt, relevantAt);
    }
}
