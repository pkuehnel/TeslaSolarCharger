using System;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;
using Xunit.Abstractions;
using Moq;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class CalculateEnergyToChargeTests : TestBase
{
    public CalculateEnergyToChargeTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    // 1. Normal case: Target 80%, Current 50%, Usable 75kWh, Loss 0%
    // Diff = 30. Energy = 30 * 75 * 10 = 22500 Wh.
    [InlineData(80, 50, 75, 0, 22500)]

    // 2. With Loss 10%
    // 22500 * 1.1 = 24750 Wh.
    [InlineData(80, 50, 75, 10, 24750)]

    // 3. Target < Current
    [InlineData(50, 80, 75, 0, 0)]

    // 4. Target == Current
    [InlineData(50, 50, 75, 0, 0)]

    // 5. Usable Energy 0
    [InlineData(80, 50, 0, 0, 0)]

    // 6. Negative Usable Energy (Should result in negative energy -> 0)
    [InlineData(80, 50, -75, 0, 0)]

    // 7. Large values: 100% of 1000kWh = 1000000 Wh.
    [InlineData(100, 0, 1000, 0, 1000000)]

    // 8. Loss 100% (Factor 2.0)
    // 22500 * 2 = 45000 Wh.
    [InlineData(80, 50, 75, 100, 45000)]

    // 9. Rounding check. Loss 15% (Factor 1.15)
    // 22500 * 1.15 = 25875 Wh.
    [InlineData(80, 50, 75, 15, 25875)]
    public void CalculateEnergyToCharge_Scenarios(
        int chargingTargetSoc,
        int currentSoC,
        int usableEnergy,
        int carChargeLoss,
        int expectedEnergyWh)
    {
        // Arrange
        Mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.CarChargeLoss())
            .Returns(carChargeLoss);

        var service = Mock.Create<ChargingScheduleService>();

        // Act
        var result = service.CalculateEnergyToCharge(chargingTargetSoc, currentSoC, usableEnergy);

        // Assert
        Assert.Equal(expectedEnergyWh, result);
    }
}
