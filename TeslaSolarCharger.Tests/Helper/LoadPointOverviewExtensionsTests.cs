using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Shared.Dtos.Home;
using Xunit;

namespace TeslaSolarCharger.Tests.Helper;

public class LoadPointOverviewExtensionsTests
{
    [Fact]
    public void MinPowerToCountAsDrawingPower_Is100()
    {
        Assert.Equal(100, LoadPointOverviewExtensions.MinPowerToCountAsDrawingPower);
    }

    [Theory]
    // Unknown power or an idle charger
    [InlineData(null, false)]
    [InlineData(0, false)]
    // Standby power reported by idle chargers, e.g. 4W from logfile 2026-10-25
    [InlineData(4, false)]
    [InlineData(99, false)]
    // From the threshold on the loadpoint draws power
    [InlineData(100, true)]
    [InlineData(101, true)]
    // Minimum charging power (2A on 3 phases) and full power
    [InlineData(1_374, true)]
    [InlineData(11_040, true)]
    // Negative values must not count as drawing power
    [InlineData(-500, false)]
    public void IsDrawingPower_ReturnsExpectedValue(int? chargingPower, bool expected)
    {
        var loadPoint = new DtoLoadPointOverview { ChargingPower = chargingPower, };

        Assert.Equal(expected, loadPoint.IsDrawingPower());
    }
}
