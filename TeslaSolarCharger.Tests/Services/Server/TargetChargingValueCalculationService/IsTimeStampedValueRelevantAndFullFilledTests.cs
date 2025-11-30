using System;
using System.Collections.Generic;
using Autofac.Extras.Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;
using TeslaSolarCharger.Tests;

namespace TeslaSolarCharger.Tests.Services.Server.TargetChargingValueCalculationService;

public class IsTimeStampedValueRelevantAndFullFilledTests : TestBase
{
    private readonly ITestOutputHelper _outputHelper;

    public IsTimeStampedValueRelevantAndFullFilledTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        _outputHelper = outputHelper;
    }

    public static IEnumerable<object[]> TestData =>
        new List<object[]>
        {
            // --- SCENARIO 1: LastChanged is Default (Never Changed) ---
            // If LastChanged is null, it is considered Relevant immediately.
            // Value matches Comparator -> Result True.
            new object[] {
                "LastChanged Default, Value Equal -> True",
                new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), true, // Init: 10:00, True
                (DateTimeOffset?)null, (bool?)null, // No Update
                new DateTimeOffset(2024, 1, 1, 10, 5, 0, TimeSpan.Zero), // Current: 10:05
                TimeSpan.FromMinutes(10), // Wait: 10m
                true, // Comparator
                true, // Expected Result
                (DateTimeOffset?)null // Expected RelevantAt (null means already relevant)
            },

            // LastChanged Default, Value Not Equal -> Result False.
            new object[] {
                "LastChanged Default, Value Not Equal -> False",
                new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), false, // Init: 10:00, False
                (DateTimeOffset?)null, (bool?)null, // No Update
                new DateTimeOffset(2024, 1, 1, 10, 5, 0, TimeSpan.Zero), // Current: 10:05
                TimeSpan.FromMinutes(10),
                true, // Comparator (expecting true)
                false, // Expected Result
                (DateTimeOffset?)null // Expected RelevantAt
            },

            // --- SCENARIO 2: LastChanged Exists, Old Enough (Relevant) ---
            // Update happened at 10:00. Wait 10m. Current is 10:11.
            // Threshold = 10:11 - 10m = 10:01.
            // LastChanged (10:00) < Threshold (10:01) -> Relevant.
            new object[] {
                "LastChanged Old Enough, Value Equal -> True",
                new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.Zero), false, // Init: 09:00, False
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), (bool?)true, // Update: 10:00, True
                new DateTimeOffset(2024, 1, 1, 10, 11, 0, TimeSpan.Zero), // Current: 10:11
                TimeSpan.FromMinutes(10), // Wait 10m
                true, // Comparator
                true, // Expected Result
                (DateTimeOffset?)null // Expected RelevantAt
            },

            // --- SCENARIO 3: LastChanged Exists, Too Recent (Not Relevant) ---
            // Update at 10:00. Wait 10m. Current is 10:05.
            // Threshold = 10:05 - 10m = 09:55.
            // LastChanged (10:00) >= Threshold (09:55) -> Not Relevant.
            // RelevantAt = LastChanged (10:00) + Wait (10m) = 10:10.
            new object[] {
                "LastChanged Too Recent, Value Equal -> False",
                new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.Zero), false, // Init
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), (bool?)true, // Update: 10:00
                new DateTimeOffset(2024, 1, 1, 10, 5, 0, TimeSpan.Zero), // Current: 10:05
                TimeSpan.FromMinutes(10), // Wait 10m
                true, // Comparator
                false, // Expected Result
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 10, 10, 0, TimeSpan.Zero) // Expected RelevantAt
            },

             // --- SCENARIO 4: LastChanged Exists, Too Recent, Value Not Equal ---
            // Update at 10:00 to False. Wait 10m. Current 10:05.
            // Not Relevant AND Not Equal.
            new object[] {
                "LastChanged Too Recent, Value Not Equal -> False",
                new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.Zero), true, // Init
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), (bool?)false, // Update: 10:00 to False
                new DateTimeOffset(2024, 1, 1, 10, 5, 0, TimeSpan.Zero), // Current: 10:05
                TimeSpan.FromMinutes(10),
                true, // Comparator
                false, // Result
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 10, 10, 0, TimeSpan.Zero) // RelevantAt should still be calculated
            },

            // --- SCENARIO 5: Exact Boundary Conditions ---
            // Update 10:00. Wait 10m. Current 10:10.
            // Threshold = 10:10 - 10m = 10:00.
            // LastChanged (10:00) < Threshold (10:00) is FALSE. (10:00 is not less than 10:00)
            // So NOT RELEVANT yet.
            new object[] {
                "LastChanged Exactly at Threshold -> False (Not Relevant)",
                new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.Zero), false,
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), (bool?)true, // Update 10:00
                new DateTimeOffset(2024, 1, 1, 10, 10, 0, TimeSpan.Zero), // Current 10:10
                TimeSpan.FromMinutes(10),
                true,
                false, // Result
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 10, 10, 0, TimeSpan.Zero) // RelevantAt = 10:00 + 10m = 10:10
            },

            // Just past threshold: Current 10:10:00 + 1 tick.
            // Threshold = 10:00:00 + 1 tick. LastChanged 10:00 < 10:00...1 -> True.
            new object[] {
                "LastChanged Just Past Threshold -> True",
                new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.Zero), false,
                (DateTimeOffset?)new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), (bool?)true, // Update 10:00
                new DateTimeOffset(2024, 1, 1, 10, 10, 0, TimeSpan.Zero).AddTicks(1), // Current 10:10 + 1 tick
                TimeSpan.FromMinutes(10),
                true,
                true, // Result
                (DateTimeOffset?)null // RelevantAt
            }
        };

    [Theory]
    [MemberData(nameof(TestData))]
    public void IsTimeStampedValueRelevantAndFullFilled_Scenarios(
        string description,
        DateTimeOffset initialTimestamp, bool initialValue,
        DateTimeOffset? updateTimestamp, bool? updateValue,
        DateTimeOffset currentDate,
        TimeSpan timeSpanUntilIsRelevant,
        bool comparator,
        bool expectedResult,
        DateTimeOffset? expectedRelevantAt)
    {
        _outputHelper.WriteLine($"Running scenario: {description}");

        // Arrange
        // Create the Dto. LastChanged is initially null.
        var dto = new DtoTimeStampedValue<bool>(initialTimestamp, initialValue);

        // If update parameters are provided, simulate an update to set LastChanged.
        // DtoTimeStampedValue only updates LastChanged if the value is different.
        if (updateTimestamp.HasValue && updateValue.HasValue)
        {
            dto.Update(updateTimestamp.Value, updateValue.Value);
        }

        // Create SUT using TestBase mock creation
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // Act
        var result = sut.IsTimeStampedValueRelevantAndFullFilled(dto, currentDate, timeSpanUntilIsRelevant, comparator, out var relevantAt);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedRelevantAt, relevantAt);
    }
}
