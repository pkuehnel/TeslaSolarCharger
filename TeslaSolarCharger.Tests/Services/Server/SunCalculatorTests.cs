using System;
using System.Collections.Generic;
using TeslaSolarCharger.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class SunCalculatorTests : TestBase
{
    public SunCalculatorTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private SunCalculator CreateSut()
    {
        return Mock.Create<SunCalculator>();
    }

    public static IEnumerable<object[]> WorkingSunriseData =>
        new List<object[]>
        {
            new object[] { new DateTimeOffset(2023, 6, 21, 0, 0, 0, TimeSpan.Zero), 3, 19 },
            new object[] { new DateTimeOffset(2023, 9, 23, 0, 0, 0, TimeSpan.Zero), 5, 9 },
            new object[] { new DateTimeOffset(2023, 12, 21, 0, 0, 0, TimeSpan.Zero), 7, 12 },
        };

    public static IEnumerable<object[]> BuggySunriseData =>
        new List<object[]>
        {
            new object[] { new DateTimeOffset(2023, 2, 2, 0, 0, 0, TimeSpan.Zero), 6, 51 },
            new object[] { new DateTimeOffset(2023, 3, 20, 0, 0, 0, TimeSpan.Zero), 5, 26 },
        };

    public static IEnumerable<object[]> AllSunsetData =>
        new List<object[]>
        {
            new object[] { new DateTimeOffset(2023, 2, 2, 0, 0, 0, TimeSpan.Zero), 16, 21 },
            new object[] { new DateTimeOffset(2023, 3, 20, 0, 0, 0, TimeSpan.Zero), 17, 34 },
            new object[] { new DateTimeOffset(2023, 6, 21, 0, 0, 0, TimeSpan.Zero), 19, 28 },
            new object[] { new DateTimeOffset(2023, 9, 23, 0, 0, 0, TimeSpan.Zero), 17, 18 },
            new object[] { new DateTimeOffset(2023, 12, 21, 0, 0, 0, TimeSpan.Zero), 15, 28 },
        };

    [Theory]
    [MemberData(nameof(WorkingSunriseData))]
    public void NextSunrise_StandardLocation_ReturnsCorrectTime(DateTimeOffset from, int expectedHour, int expectedMinute)
    {
        // Arrange
        var sut = CreateSut();
        var latitude = 48.653180;
        var longitude = 9.449150;
        var expectedTime = from.Date.AddHours(expectedHour).AddMinutes(expectedMinute);
        var tolerance = TimeSpan.FromMinutes(10);

        // Act
        var result = sut.NextSunrise(latitude, longitude, from, 10);

        // Assert
        Assert.NotNull(result);
        var difference = result.Value - expectedTime;
        Assert.True(Math.Abs(difference.TotalMinutes) <= tolerance.TotalMinutes,
            $"Expected sunrise around {expectedTime:HH:mm} UTC, but got {result.Value:HH:mm} UTC. Difference: {difference.TotalMinutes} mins");
    }

    [Theory]
    [MemberData(nameof(AllSunsetData))]
    public void NextSunset_StandardLocation_ReturnsCorrectTime(DateTimeOffset from, int expectedHour, int expectedMinute)
    {
        // Arrange
        var sut = CreateSut();
        var latitude = 48.653180;
        var longitude = 9.449150;
        var expectedTime = from.Date.AddHours(expectedHour).AddMinutes(expectedMinute);
        var tolerance = TimeSpan.FromMinutes(10);

        // Act
        var result = sut.NextSunset(latitude, longitude, from, 10);

        // Assert
        Assert.NotNull(result);
        var difference = result.Value - expectedTime;
        Assert.True(Math.Abs(difference.TotalMinutes) <= tolerance.TotalMinutes,
            $"Expected sunset around {expectedTime:HH:mm} UTC, but got {result.Value:HH:mm} UTC. Difference: {difference.TotalMinutes} mins");
    }

    [Theory]
    [MemberData(nameof(BuggySunriseData))]
    public void NextSunrise_BuggyDates_FailsIntentionally(DateTimeOffset from, int expectedHour, int expectedMinute)
    {
        // Arrange
        var sut = CreateSut();
        var latitude = 48.653180;
        var longitude = 9.449150;
        var expectedTime = from.Date.AddHours(expectedHour).AddMinutes(expectedMinute);
        var tolerance = TimeSpan.FromMinutes(10);

        // Act
        var result = sut.NextSunrise(latitude, longitude, from, 10);

        // Assert
        Assert.NotNull(result);
        // This assertion fails because the code returns the next day's sunrise for these dates
        // Comment indicating intentional failure due to bug in SunCalculator (missing > 24h normalization)
        var difference = result.Value - expectedTime;
        Assert.True(Math.Abs(difference.TotalMinutes) <= tolerance.TotalMinutes,
            $"[INTENTIONAL FAILURE] Expected sunrise around {expectedTime:HH:mm} UTC, but got {result.Value:HH:mm} UTC (Likely next day). Difference: {difference.TotalMinutes} mins");
    }

    [Fact]
    public void NextSunset_PolarDay_ReturnsFutureDateAfterPolarDayEnds()
    {
        var sut = CreateSut();
        var from = new DateTimeOffset(2023, 6, 21, 0, 0, 0, TimeSpan.Zero);
        var latitude = 69.6492;
        var longitude = 18.9553;

        var result = sut.NextSunset(latitude, longitude, from, 400);

        Assert.NotNull(result);
        Assert.True((result.Value - from).TotalDays > 20, "Sunset should be weeks away during polar day");
        Assert.Equal(7, result.Value.Month);
    }

    [Fact]
    public void NextSunrise_PolarNight_ReturnsFutureDateAfterPolarNightEnds()
    {
        var sut = CreateSut();
        var from = new DateTimeOffset(2023, 12, 21, 0, 0, 0, TimeSpan.Zero);
        var latitude = 69.6492;
        var longitude = 18.9553;

        var result = sut.NextSunrise(latitude, longitude, from, 400);

        Assert.NotNull(result);
        Assert.True((result.Value - from).TotalDays > 10, "Sunrise should be weeks away during polar night");
        Assert.Equal(2024, result.Value.Year);
        Assert.Equal(1, result.Value.Month);
    }

    [Fact]
    public void NextSunrise_RespectsMaxFutureDays_FailsIntentionally()
    {
        var sut = CreateSut();
        var from = new DateTimeOffset(2023, 12, 21, 0, 0, 0, TimeSpan.Zero);
        var latitude = 69.6492;
        var longitude = 18.9553;
        var maxFutureDays = 5;

        var result = sut.NextSunrise(latitude, longitude, from, maxFutureDays);

        // Assert
        // This fails intentionally because the current implementation ignores maxFutureDays (uses hardcoded 400 loop)
        Assert.Null(result);
    }
}
