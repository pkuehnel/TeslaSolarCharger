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
    [InlineData("tesla", 2)]
    [InlineData("TESLA MODEL 3", 1)]
    [InlineData("  model 3  ", 1)]
    [InlineData("i4", 1)]
    [InlineData("bmw i4", 1)]
    [InlineData("Polestar", 0)]
    [InlineData("", 3)]
    [InlineData("   ", 3)]
    public void Search_FiltersByMakeAndModelIgnoringCase(string query, int expectedCount)
    {
        var vehicles = new List<DtoSmartCarCompatibleVehicle>
        {
            Vehicle("Tesla", "Model 3"),
            Vehicle("Tesla", "Model Y"),
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
            Vehicle("Tesla", "Model Y"),
            Vehicle("BMW", "i4"),
            Vehicle("Tesla", "Model 3"),
        };

        var result = SmartCarCompatibleVehicleSearch.Search(vehicles, null)
            .Select(v => $"{v.Make} {v.Model}")
            .ToList();

        Assert.Equal(new[] { "BMW i4", "Tesla Model 3", "Tesla Model Y" }, result);
    }

    [Fact]
    public void Search_EmptySource_ReturnsEmpty()
    {
        Assert.Empty(SmartCarCompatibleVehicleSearch.Search(new List<DtoSmartCarCompatibleVehicle>(), "tesla"));
    }
}
