using System;
using System.Linq;
using TeslaSolarCharger.Client.Components.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Localization;
using Xunit;

namespace TeslaSolarCharger.Tests.Components;

/// <summary>
/// The one description of each way to reach a car that the setup assistant, the equipment list and the car settings
/// page all read from.
/// </summary>
public class SetupCarConnectionRoutesTests
{
    private static readonly SetupCarConnectionRoute[] DecidedRoutes = Enum.GetValues<SetupCarConnectionRoute>()
        .Where(r => r != SetupCarConnectionRoute.Undecided)
        .ToArray();

    [Fact]
    public void EveryRouteBelongsToExactlyOneKindOfCar()
    {
        var grouped = SetupCarConnectionRoutes.TeslaRoutes.Concat(SetupCarConnectionRoutes.OtherCarRoutes).ToList();

        //A route in neither list would never be offered, a route in both would be offered to cars it cannot reach.
        Assert.Equal(DecidedRoutes.OrderBy(r => r), grouped.OrderBy(r => r));
        Assert.Equal(grouped.Count, grouped.Distinct().Count());
    }

    [Fact]
    public void TheUndecidedRouteIsOfferedToNobody()
    {
        Assert.DoesNotContain(SetupCarConnectionRoute.Undecided, SetupCarConnectionRoutes.TeslaRoutes);
        Assert.DoesNotContain(SetupCarConnectionRoute.Undecided, SetupCarConnectionRoutes.OtherCarRoutes);
    }

    [Theory]
    [InlineData(SetupCarConnectionRoute.TeslaBluetooth, false)]
    [InlineData(SetupCarConnectionRoute.TeslaCloud, true)]
    [InlineData(SetupCarConnectionRoute.SmartCarWithChargingStation, true)]
    [InlineData(SetupCarConnectionRoute.ChargingStationOnly, false)]
    [InlineData(SetupCarConnectionRoute.Undecided, false)]
    public void OnlyTheRoutesThroughAnOnlineServiceNeedASubscription(SetupCarConnectionRoute route, bool expected)
    {
        Assert.Equal(expected, SetupCarConnectionRoutes.RequiresSubscription(route));
    }

    [Fact]
    public void EveryDecidedRouteHasItsOwnNameDescriptionAndRequirement()
    {
        var names = DecidedRoutes.Select(SetupCarConnectionRoutes.NameKey).ToList();
        var descriptions = DecidedRoutes.Select(SetupCarConnectionRoutes.DescriptionKey).ToList();
        var requirements = DecidedRoutes.Select(SetupCarConnectionRoutes.RequirementKey).ToList();

        Assert.DoesNotContain(TranslationKeys.SetupRouteNotDecidedYet, names);
        Assert.DoesNotContain(null, descriptions);
        Assert.DoesNotContain(null, requirements);
        //Two routes sharing a text would read as the same option twice.
        Assert.Equal(DecidedRoutes.Length, names.Distinct().Count());
        Assert.Equal(DecidedRoutes.Length, descriptions.Distinct().Count());
        Assert.Equal(DecidedRoutes.Length, requirements.Distinct().Count());
    }

    [Fact]
    public void AnUndecidedRouteIsNamedAsSuchAndDescribesNothing()
    {
        Assert.Equal(TranslationKeys.SetupRouteNotDecidedYet, SetupCarConnectionRoutes.NameKey(SetupCarConnectionRoute.Undecided));
        Assert.Null(SetupCarConnectionRoutes.DescriptionKey(SetupCarConnectionRoute.Undecided));
        Assert.Null(SetupCarConnectionRoutes.RequirementKey(SetupCarConnectionRoute.Undecided));
    }

    [Fact]
    public void AValueOutsideTheKnownRoutesIsTreatedAsUndecided()
    {
        //A route stored by a newer build must not be described as one of the routes this build knows.
        var unknown = (SetupCarConnectionRoute)99;

        Assert.Equal(TranslationKeys.SetupRouteNotDecidedYet, SetupCarConnectionRoutes.NameKey(unknown));
        Assert.Null(SetupCarConnectionRoutes.DescriptionKey(unknown));
        Assert.Null(SetupCarConnectionRoutes.RequirementKey(unknown));
        Assert.False(SetupCarConnectionRoutes.RequiresSubscription(unknown));
        Assert.False(SetupCarConnectionRoutes.IsDecided(unknown));
    }

    [Fact]
    public void OnlyTheRoutesACarCanTakeCountAsDecided()
    {
        Assert.All(DecidedRoutes, route => Assert.True(SetupCarConnectionRoutes.IsDecided(route)));
        Assert.False(SetupCarConnectionRoutes.IsDecided(SetupCarConnectionRoute.Undecided));
    }
}
