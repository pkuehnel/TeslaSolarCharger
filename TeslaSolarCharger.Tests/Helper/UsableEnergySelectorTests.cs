using System.Collections.Generic;
using TeslaSolarCharger.Shared.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Helper;

public class UsableEnergySelectorTests
{
    [Theory]
    [InlineData(true, 9_500, 10_000, 9_500)]
    [InlineData(false, 9_500, 10_000, 10_000)]
    [InlineData(true, null, 10_000, 10_000)]
    [InlineData(true, 0, 10_000, 10_000)]
    [InlineData(true, 9_500, null, 9_500)]
    [InlineData(false, 9_500, null, null)]
    public void SelectWh_PrefersTheLearnedValueWhileEnabledAndKnown(bool useLearned, int? learnedWh, int? enteredWh, int? expected)
    {
        Assert.Equal(expected, UsableEnergySelector.SelectWh(useLearned, learnedWh, enteredWh));
    }

    [Theory]
    [InlineData(60_499, 60)]
    [InlineData(60_500, 61)]
    [InlineData(57_000, 57)]
    public void SelectCarKwh_RoundsTheLearnedValueToWholeKwh(int learnedWh, int expectedKwh)
    {
        var learned = new Dictionary<int, int> { [1] = learnedWh, };

        Assert.Equal(expectedKwh, UsableEnergySelector.SelectCarKwh(true, learned, 1, 75));
    }

    [Fact]
    public void SelectCarKwh_WithoutLearnedValueForTheCar_UsesTheEnteredValue()
    {
        var learned = new Dictionary<int, int> { [2] = 60_000, };

        Assert.Equal(75, UsableEnergySelector.SelectCarKwh(true, learned, 1, 75));
        Assert.Equal(75, UsableEnergySelector.SelectCarKwh(true, null, 1, 75));
    }

    [Fact]
    public void SelectCarKwh_WithLearningSwitchedOff_UsesTheEnteredValue()
    {
        var learned = new Dictionary<int, int> { [1] = 60_000, };

        Assert.Equal(75, UsableEnergySelector.SelectCarKwh(false, learned, 1, 75));
    }
}
