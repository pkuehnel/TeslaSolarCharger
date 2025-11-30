using System;
using Xunit;
using Xunit.Abstractions;
using TeslaSolarCharger.Server.Services;

namespace TeslaSolarCharger.Tests.Services.Server.TargetChargingValueCalculationService
{
    public class RecalculatePowerToControlValuesTests : TestBase
    {
        public RecalculatePowerToControlValuesTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [Theory]
        // 1. Consumption fully covered by battery discharge
        [InlineData(10000, 5000, 2000, 10000, 3000)]
        // 2. Consumption exactly matches battery discharge
        [InlineData(10000, 2000, 2000, 10000, 0)]
        // 3. Consumption exceeds battery discharge (spills to powerToControl)
        [InlineData(10000, 2000, 3000, 9000, 0)]
        // 4. Zero consumption
        [InlineData(10000, 2000, 0, 10000, 2000)]
        // 5. No battery discharge available (purely reduces powerToControl)
        [InlineData(10000, 0, 2000, 8000, 0)]
        // 6. Everything is zero
        [InlineData(0, 0, 0, 0, 0)]
        // 7. Negative powerToControl (consumption covered by battery)
        [InlineData(-1000, 2000, 500, -1000, 1500)]
        // 8. Negative powerToControl with spillover
        [InlineData(-1000, 500, 1000, -1500, 0)]
        // 9. Large values
        [InlineData(100000, 50000, 25000, 100000, 25000)]
        public void RecalculatePowerToControlValues_CalculatesCorrectly(
            int initialPowerToControl,
            int initialAdditionalBatteryDischarge,
            int estimatedUsage,
            int expectedPowerToControl,
            int expectedAdditionalBatteryDischarge)
        {
            // Arrange
            var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

            // Act
            var (actualPowerToControl, actualAdditionalBatteryDischarge) = service.RecalculatePowerToControlValues(
                initialPowerToControl,
                initialAdditionalBatteryDischarge,
                estimatedUsage);

            // Assert
            Assert.Equal(expectedPowerToControl, actualPowerToControl);
            Assert.Equal(expectedAdditionalBatteryDischarge, actualAdditionalBatteryDischarge);
        }

        [Fact]
        public void RecalculatePowerToControlValues_NegativeUsage_DoesNothing()
        {
            // If estimatedUsage is negative, the current logic does nothing because (remainingUsage > 0) is false.
            // Arrange
            var service = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();
            int initialPower = 1000;
            int initialBattery = 500;
            int negativeUsage = -100;

            // Act
            var (actualPower, actualBattery) = service.RecalculatePowerToControlValues(initialPower, initialBattery, negativeUsage);

            // Assert
            Assert.Equal(initialPower, actualPower);
            Assert.Equal(initialBattery, actualBattery);
        }
    }
}
