using System.Collections.Generic;
using System.Linq;
using TeslaSolarCharger.Client.Helper;
using TeslaSolarCharger.Shared.Dtos;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Client;

public class SmartCarCompatibleVehicleSearchTests
{
    private static DtoSmartCarCompatibleVehicle Vehicle(string make, string model) =>
        new() { Make = make, Model = model, Region = "EUROPE", };

    [Fact]
    public void Search_EmptyQuery_ReturnsAllVehiclesWithoutLimit()
    {
        var vehicles = Enumerable.Range(0, 120).Select(i => Vehicle("Make", $"Model {i:D3}")).ToList();

        var result = SmartCarCompatibleVehicleSearch.Search(vehicles, null).ToList();

        Assert.Equal(120, result.Count);
    }

    [Theory]
    [InlineData("kia", 2)]
    [InlineData("KIA EV6", 1)]
    [InlineData("  ev6  ", 1)]
    [InlineData("i4", 1)]
    [InlineData("bmw i4", 1)]
    [InlineData("Polestar", 0)]
    [InlineData("", 3)]
    [InlineData("   ", 3)]
    public void Search_FiltersByMakeAndModelIgnoringCase(string query, int expectedCount)
    {
        var vehicles = new List<DtoSmartCarCompatibleVehicle>
        {
            Vehicle("Kia", "EV6"),
            Vehicle("Kia", "EV9"),
            Vehicle("BMW", "i4"),
        };

        var result = SmartCarCompatibleVehicleSearch.Search(vehicles, query).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void Search_SortsByMakeThenModel()
    {
        var vehicles = new List<DtoSmartCarCompatibleVehicle>
        {
            Vehicle("Kia", "EV9"),
            Vehicle("BMW", "i4"),
            Vehicle("Kia", "EV6"),
        };

        var result = SmartCarCompatibleVehicleSearch.Search(vehicles, null)
            .Select(v => $"{v.Make} {v.Model}")
            .ToList();

        Assert.Equal(new[] { "BMW i4", "Kia EV6", "Kia EV9" }, result);
    }

    [Fact]
    public void Search_EmptySource_ReturnsEmpty()
    {
        Assert.Empty(SmartCarCompatibleVehicleSearch.Search(new List<DtoSmartCarCompatibleVehicle>(), "kia"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tesla")]
    [InlineData("model")]
    public void Search_NeverReturnsTeslas(string? query)
    {
        // SmartCar reports the make in upper case; the filter must not depend on the casing.
        var vehicles = new List<DtoSmartCarCompatibleVehicle>
        {
            Vehicle("TESLA", "Model 3"),
            Vehicle("Tesla", "Model Y"),
            Vehicle("tesla", "Model S"),
        };

        Assert.Empty(SmartCarCompatibleVehicleSearch.Search(vehicles, query));
    }

    [Fact]
    public void Search_KeepsOtherMakesNextToTeslas()
    {
        var vehicles = new List<DtoSmartCarCompatibleVehicle>
        {
            Vehicle("TESLA", "Model 3"),
            Vehicle("BMW", "i4"),
        };

        var result = SmartCarCompatibleVehicleSearch.Search(vehicles, null).ToList();

        Assert.Equal("BMW", Assert.Single(result).Make);
    }
}
