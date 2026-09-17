using System.Linq;
using TeslaSolarCharger.Client.Components.Setup;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The addresses of the setup screens and the stable steps behind them. These two have to stay in step: the address
/// is what a browser reload and an account return come back to, the step is what gets persisted.
/// </summary>
public class SetupSectionsTests
{
    [Theory]
    [InlineData(SetupSections.Welcome, SetupStepKey.Welcome)]
    [InlineData(SetupSections.Account, SetupStepKey.CloudConnection)]
    [InlineData(SetupSections.Solar, SetupStepKey.SolarAndBattery)]
    [InlineData(SetupSections.Location, SetupStepKey.Location)]
    [InlineData(SetupSections.Prices, SetupStepKey.Prices)]
    [InlineData(SetupSections.Equipment, SetupStepKey.CarsAndCharging)]
    [InlineData(SetupSections.Finish, SetupStepKey.Finish)]
    public void EverySectionMapsToItsStoredStep(string section, SetupStepKey expected)
    {
        Assert.Equal(expected, SetupSections.ToStepKey(section));
    }

    [Theory]
    [InlineData(SetupSections.Car)]
    [InlineData(SetupSections.Charger)]
    public void SettingUpOneDeviceCountsAsDescribingTheEquipment(string section)
    {
        //A car and a charger are detours off the equipment list, so they share its stored step rather than
        //introducing steps a stored state from an older build would not know.
        Assert.Equal(SetupStepKey.CarsAndCharging, SetupSections.ToStepKey(section));
        Assert.True(SetupSections.IsDeviceSection(section));
    }

    [Fact]
    public void AnAddressThisBuildDoesNotKnowIsNotMistakenForAStep()
    {
        Assert.Equal(SetupStepKey.Unknown, SetupSections.ToStepKey("something-else"));
        Assert.Equal(SetupStepKey.Unknown, SetupSections.ToStepKey(null));
    }

    [Fact]
    public void AStoredStepThisBuildDoesNotKnowFallsBackToTheStart()
    {
        Assert.Equal(SetupSections.Welcome, SetupSections.ToSection(SetupStepKey.Unknown));
    }

    [Fact]
    public void EveryStoredStepHasASectionToShowIt()
    {
        var steps = new[]
        {
            SetupStepKey.Welcome, SetupStepKey.CloudConnection, SetupStepKey.SolarAndBattery,
            SetupStepKey.Location, SetupStepKey.Prices, SetupStepKey.CarsAndCharging, SetupStepKey.Finish,
        };

        Assert.All(steps, step => Assert.Contains(SetupSections.ToSection(step), SetupSections.Order));
    }

    [Fact]
    public void TheOrderRunsFromWelcomeToFinish()
    {
        Assert.Equal(SetupSections.Welcome, SetupSections.Order.First());
        Assert.Equal(SetupSections.Finish, SetupSections.Order.Last());
    }

    [Fact]
    public void TheEndsOfTheJourneyHaveNowhereFurtherToGo()
    {
        Assert.Null(SetupSections.Previous(SetupSections.Welcome));
        Assert.Null(SetupSections.Next(SetupSections.Finish));
    }

    [Fact]
    public void MovingForwardAndBackAgainReturnsToTheSameSection()
    {
        foreach (var section in SetupSections.Order)
        {
            var next = SetupSections.Next(section);
            if (next == null)
            {
                continue;
            }

            Assert.Equal(section, SetupSections.Previous(next));
        }
    }

    [Fact]
    public void ADeviceDetourIsNotAStopOnTheJourney()
    {
        //Being on a car's screen must not make "next" mean the next section of the assistant.
        Assert.DoesNotContain(SetupSections.Car, SetupSections.Order);
        Assert.DoesNotContain(SetupSections.Charger, SetupSections.Order);
    }
}
