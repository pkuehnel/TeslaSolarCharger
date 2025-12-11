using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class GetHomeBatteryEnergyFromSocDifferenceTests : TestBase
{
    public GetHomeBatteryEnergyFromSocDifferenceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    /// <summary>
    /// Verifies that GetHomeBatteryEnergyFromSocDifference calculates the energy value correctly
    /// based on the home battery capacity and the SoC difference.
    /// Covers scenarios:
    /// - Home battery capacity not set (null or 0)
    /// - SoC difference <= 0
    /// - Standard positive calculations
    /// - Truncation behavior for integer division
    /// </summary>
    /// <param name="homeBatteryUsableEnergy">The configured usable energy of the home battery in Wh.</param>
    /// <param name="socDifference">The difference in SoC (percentage).</param>
    /// <param name="expectedEnergy">The expected calculated energy in Wh.</param>
    [Theory]
    [InlineData(null, 10, 0)]
    [InlineData(0, 10, 0)]
    [InlineData(10000, -10, 0)]
    [InlineData(10000, 0, 0)]
    [InlineData(10000, 10, 1000)]
    [InlineData(13500, 50, 6750)]
    [InlineData(10000, 100, 10000)]
    [InlineData(10099, 1, 100)]
    public void GetHomeBatteryEnergyFromSocDifference_CalculatesCorrectly(int? homeBatteryUsableEnergy, int socDifference, int expectedEnergy)
    {
        // Arrange
        Mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.HomeBatteryUsableEnergy())
            .Returns(homeBatteryUsableEnergy);

        var service = Mock.Create<ChargingScheduleService>();

        // Act
        var result = service.GetHomeBatteryEnergyFromSocDifference(socDifference);

        // Assert
        Assert.Equal(expectedEnergy, result);
    }
}
