using System;
using Xunit;
using Xunit.Abstractions;
using TeslaSolarCharger.Server.Services;
using Autofac.Extras.Moq;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class CalculateChargingDurationTests : TestBase
{
    public CalculateChargingDurationTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    /// <summary>
    /// Verifies that CalculateChargingDuration returns the correct TimeSpan based on energy and power.
    /// This test covers various scenarios including standard calculations, edge cases with zero/negative values, and decimal results.
    /// </summary>
    /// <param name="energyToChargeWh">The energy to charge in Wh.</param>
    /// <param name="maxChargingPowerW">The charging power in W.</param>
    /// <param name="expectedDurationHours">The expected duration in hours.</param>
    [Theory]
    [InlineData(1000, 1000, 1.0)]      // 1000Wh / 1000W = 1 hour
    [InlineData(500, 1000, 0.5)]       // 500Wh / 1000W = 0.5 hours
    [InlineData(2000, 1000, 2.0)]      // 2000Wh / 1000W = 2 hours
    [InlineData(0, 1000, 0.0)]         // 0Wh / 1000W = 0 hours
    [InlineData(100, 300, 0.333333)]   // 100Wh / 300W = 0.33... hours (Repeating decimal)
    [InlineData(-1000, 1000, -1.0)]    // Negative energy (Edge case)
    [InlineData(1000, -1000, -1.0)]    // Negative power (Edge case)
    [InlineData(-1000, -1000, 1.0)]    // Both negative (Edge case)
    public void CalculateChargingDuration_CalculatesCorrectly(int energyToChargeWh, double maxChargingPowerW, double expectedDurationHours)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Act
        var result = service.CalculateChargingDuration(energyToChargeWh, maxChargingPowerW);

        // Assert
        Assert.Equal(expectedDurationHours, result.TotalHours, 5);
    }

    /// <summary>
    /// Verifies that CalculateChargingDuration throws OverflowException when power is 0 and energy is non-zero.
    /// Dividing by zero results in Infinity, which causes TimeSpan.FromHours to throw OverflowException.
    /// </summary>
    [Fact]
    public void CalculateChargingDuration_ThrowsOverflowException_WhenPowerIsZeroAndEnergyIsNonZero()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var energyToChargeWh = 1000;
        var maxChargingPowerW = 0.0;

        // Act & Assert
        Assert.Throws<OverflowException>(() => service.CalculateChargingDuration(energyToChargeWh, maxChargingPowerW));
    }

    /// <summary>
    /// Verifies that CalculateChargingDuration throws ArgumentException when power is 0 and energy is 0.
    /// 0 divided by 0 results in NaN, which causes TimeSpan.FromHours to throw ArgumentException.
    /// </summary>
    [Fact]
    public void CalculateChargingDuration_ThrowsArgumentException_WhenPowerIsZeroAndEnergyIsZero()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var energyToChargeWh = 0;
        var maxChargingPowerW = 0.0;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.CalculateChargingDuration(energyToChargeWh, maxChargingPowerW));
    }
}
