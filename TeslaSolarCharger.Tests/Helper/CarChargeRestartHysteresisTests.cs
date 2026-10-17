using TeslaSolarCharger.Server.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Helper;

public class CarChargeRestartHysteresisTests
{
    [Fact]
    public void Percent_IsThree()
    {
        Assert.Equal(3, CarChargeRestartHysteresis.Percent);
    }

    [Theory]
    // Within the hysteresis below the limit the car does not start again
    [InlineData(79, 80, true)]
    [InlineData(78, 80, true)]
    // At or above the limit the car does not start either
    [InlineData(80, 80, true)]
    [InlineData(85, 80, true)]
    // Exactly the hysteresis below the limit the car starts again
    [InlineData(77, 80, false)]
    [InlineData(50, 80, false)]
    [InlineData(97, 100, false)]
    [InlineData(98, 100, true)]
    // Unknown values never block
    [InlineData(null, 80, false)]
    [InlineData(78, null, false)]
    [InlineData(null, null, false)]
    public void PreventsChargeStart_ReturnsExpected(int? soc, int? socLimit, bool expected)
    {
        Assert.Equal(expected, CarChargeRestartHysteresis.PreventsChargeStart(soc, socLimit));
    }
}
