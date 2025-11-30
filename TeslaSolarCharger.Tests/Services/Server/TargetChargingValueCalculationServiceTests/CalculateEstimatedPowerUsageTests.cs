using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.TargetChargingValueCalculationServiceTests;

public class CalculateEstimatedPowerUsageTests : TestBase
{
    public CalculateEstimatedPowerUsageTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Theory]
    // 1. All null -> defaults (Voltage: 230, Phases: 3)
    // 10A * 230V * 3 = 6900
    [InlineData(10, null, null, null, null, 6900)]

    // 2. LoadPoint Voltage present -> uses it (220)
    // 10A * 220V * 3 = 6600
    [InlineData(10, 220, 240, null, null, 6600)]

    // 3. Settings Voltage present (LoadPoint null) -> uses it (240)
    // 10A * 240V * 3 = 7200
    [InlineData(10, null, 240, null, null, 7200)]

    // 4. LoadPoint Phases present -> uses it (1)
    // 10A * 230V * 1 = 2300
    [InlineData(10, null, null, 1, 3, 2300)]

    // 5. TargetValues Phases present (LoadPoint null) -> uses it (2)
    // 10A * 230V * 2 = 4600
    [InlineData(10, null, null, null, 2, 4600)]

    // 6. Complex: Estimated Voltage + Target Phases
    // 10A * 220V * 2 = 4400
    [InlineData(10, 220, null, null, 2, 4400)]

    // 7. Edge Case: 0 Amps -> 0 Power
    [InlineData(0, null, null, null, null, 0)]

    // 8. Edge Case: Voltage 0 (unlikely but possible) -> 0 Power
    [InlineData(10, 0, null, null, null, 0)]
    public void CalculateEstimatedPowerUsage_CalculatesCorrectly(
        decimal estimatedCurrentUsage,
        int? loadPointVoltage,
        int? settingsAvgVoltage,
        int? loadPointPhases,
        int? targetPhases,
        int expectedPower)
    {
        // Arrange
        Mock.Mock<ISettings>()
            .Setup(s => s.AverageHomeGridVoltage)
            .Returns(settingsAvgVoltage);

        var loadPoint = new DtoLoadPointOverview
        {
            EstimatedVoltageWhileCharging = loadPointVoltage,
            ActualPhases = loadPointPhases
        };

        var dto = new DtoTargetChargingValues(loadPoint);
        if (targetPhases.HasValue)
        {
            dto.TargetValues = new TargetValues
            {
                TargetPhases = targetPhases
            };
        }

        var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // Act
        var result = service.CalculateEstimatedPowerUsage(dto, estimatedCurrentUsage);

        // Assert
        Assert.Equal(expectedPower, result);
    }
}
