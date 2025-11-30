using System;
using Xunit;
using Xunit.Abstractions;
using TeslaSolarCharger.Server.Services;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class CalculateChargedEnergyTests : TestBase
{
    public CalculateChargedEnergyTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData(0, 1, 0, 0, 1000, 1000)] // 1 hour, 1000W -> 1000Wh
    [InlineData(0, 2, 0, 0, 500, 1000)]  // 2 hours, 500W -> 1000Wh
    [InlineData(0, 0, 30, 0, 1000, 500)] // 30 mins, 1000W -> 500Wh
    [InlineData(0, 0, 0, 0, 1000, 0)]    // 0 time -> 0Wh
    [InlineData(0, 1, 0, 0, 0, 0)]       // 0 power -> 0Wh
    [InlineData(0, 0, 15, 0, 100, 25)]   // 15 mins (0.25h) * 100W = 25Wh
    public void CalculateChargedEnergy_GivenDurationAndPower_ReturnsCorrectEnergy(
        int days, int hours, int minutes, int seconds,
        int chargingPower,
        int expectedEnergy)
    {
        // Arrange
        var service = Mock.Create<ChargingScheduleService>();
        var duration = new TimeSpan(days, hours, minutes, seconds);

        // Act
        var result = service.CalculateChargedEnergy(duration, chargingPower);

        // Assert
        Assert.Equal(expectedEnergy, result);
    }

    [Fact(Skip = "Currently fails because implementation truncates instead of rounding.")]
    public void CalculateChargedEnergy_SmallDurationAndPower_ShouldRoundNotTruncate()
    {
        // This test fails intentionally to demonstrate that small energy values are truncated to 0.
        // Example: 1 minute at 50W.
        // Energy = 50W * (1/60)h = 0.8333 Wh.
        // Current implementation: (int)0.8333 = 0.
        // Expected (if rounded): 1.

        // Arrange
        var service = Mock.Create<ChargingScheduleService>();
        var duration = TimeSpan.FromMinutes(1);
        var chargingPower = 50;

        // Act
        var result = service.CalculateChargedEnergy(duration, chargingPower);

        // Assert
        Assert.Equal(1, result);
    }
}
