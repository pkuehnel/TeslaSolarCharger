using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Tests;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class GetPowerAtPhasesAndCurrentTests : TestBase
{
    public GetPowerAtPhasesAndCurrentTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    /// <summary>
    /// Verifies that GetPowerAtPhasesAndCurrent correctly calculates power based on the formula:
    /// Power = Phases * Current * Voltage.
    /// It covers various scenarios including different phase counts, current values, and voltage (including null default).
    /// </summary>
    /// <param name="phases">Number of phases (e.g., 1 or 3).</param>
    /// <param name="current">Current in Amperes.</param>
    /// <param name="voltage">Voltage in Volts. If null, defaults to 230V.</param>
    /// <param name="expectedPower">Expected power in Watts.</param>
    [Theory]
    [InlineData(1, 10, 230, 2300)]      // 1 Phase, 10A, 230V -> 2300W
    [InlineData(3, 10, 230, 6900)]      // 3 Phases, 10A, 230V -> 6900W
    [InlineData(3, 16, 230, 11040)]     // 3 Phases, 16A, 230V -> 11040W (Common 11kW charger)
    [InlineData(1, 10, 220, 2200)]      // Custom voltage 220V
    [InlineData(3, 32, 230, 22080)]     // 3 Phases, 32A, 230V -> 22kW charger
    [InlineData(0, 10, 230, 0)]         // 0 Phases -> 0W
    [InlineData(3, 0, 230, 0)]          // 0 Current -> 0W
    [InlineData(1, 10.5, 230, 2415)]    // Decimal current is handled
    public void GetPowerAtPhasesAndCurrent_CalculatesCorrectly(int phases, decimal current, int voltage, int expectedPower)
    {
        // Arrange
        // Create an instance of ChargingScheduleService using the AutoMock container from TestBase.
        // This resolves the service with all its dependencies mocked.
        var service = Mock.Create<ChargingScheduleService>();

        // Act
        var result = service.GetPowerAtPhasesAndCurrent(phases, current, voltage);

        // Assert
        Assert.Equal(expectedPower, result);
    }
}
