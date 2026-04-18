using System;
using System.Collections.Generic;
using TeslaSolarCharger.Server.Services;
using Xunit;


namespace TeslaSolarCharger.Tests.Services.Server;

public class SunCalculatorTests : TestBase
{
    public SunCalculatorTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    private const double StandardLatitude = 48.653180;
    private const double StandardLongitude = 9.449150;

    private const double PolarLatitude = 69.6492;
    private const double PolarLongitude = 18.9553;

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
        var expectedTime = GetExpectedTime(from, expectedHour, expectedMinute);
        var tolerance = TimeSpan.FromMinutes(10);

        // Act
        var result = sut.NextSunrise(StandardLatitude, StandardLongitude, from, 10);

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
        var expectedTime = GetExpectedTime(from, expectedHour, expectedMinute);
        var tolerance = TimeSpan.FromMinutes(10);

        // Act
        var result = sut.NextSunset(StandardLatitude, StandardLongitude, from, 10);

        // Assert
        Assert.NotNull(result);
        var difference = result.Value - expectedTime;
        Assert.True(Math.Abs(difference.TotalMinutes) <= tolerance.TotalMinutes,
            $"Expected sunset around {expectedTime:HH:mm} UTC, but got {result.Value:HH:mm} UTC. Difference: {difference.TotalMinutes} mins");
    }

    private static DateTimeOffset GetExpectedTime(DateTimeOffset from, int expectedHour, int expectedMinute)
    {
        return new DateTimeOffset(from.Date.AddHours(expectedHour).AddMinutes(expectedMinute), TimeSpan.Zero);
    }

    [Fact]
    public void NextSunset_PolarDay_ReturnsFutureDateAfterPolarDayEnds()
    {
        var sut = CreateSut();
        var from = new DateTimeOffset(2023, 6, 21, 0, 0, 0, TimeSpan.Zero);

        var result = sut.NextSunset(PolarLatitude, PolarLongitude, from, 400);

        Assert.NotNull(result);
        Assert.True((result.Value - from).TotalDays > 20, "Sunset should be weeks away during polar day");
        Assert.Equal(7, result.Value.Month);
    }

    [Fact]
    public void NextSunrise_PolarNight_ReturnsFutureDateAfterPolarNightEnds()
    {
        var sut = CreateSut();
        var from = new DateTimeOffset(2023, 12, 21, 0, 0, 0, TimeSpan.Zero);

        var result = sut.NextSunrise(PolarLatitude, PolarLongitude, from, 400);

        Assert.NotNull(result);
        Assert.True((result.Value - from).TotalDays > 10, "Sunrise should be weeks away during polar night");
        Assert.Equal(2024, result.Value.Year);
        Assert.Equal(1, result.Value.Month);
    }

    [Fact]
    public void NextSunrise_RespectsMaxFutureDays()
    {
        var sut = CreateSut();
        var from = new DateTimeOffset(2023, 12, 21, 0, 0, 0, TimeSpan.Zero);
        var maxFutureDays = 5;

        var result = sut.NextSunrise(PolarLatitude, PolarLongitude, from, maxFutureDays);

        Assert.Null(result);
    }
}
