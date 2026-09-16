using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

/// <summary>
/// The name a device is offered under and the label it gets when connected. Setup no longer asks for a label, so the
/// one proposed here is what the user sees in their device list.
/// </summary>
public class TemplateValueGatherTypeVendorsTests
{
    private readonly StringHelper _stringHelper = new(NullLogger<StringHelper>.Instance);

    [Fact]
    public void TheMakeIsSpelledTheWayTheManufacturerSpellsIt()
    {
        Assert.Equal("SMA Hybrid Inverter Modbus",
            TemplateValueGatherTypeVendors.GetDisplayName(TemplateValueGatherType.SmaHybridInverterModbus, _stringHelper));
    }

    [Fact]
    public void AModelNamedWithoutItsMakeGetsTheMakeInFront()
    {
        Assert.Equal("Ads-tec Storaxe Modbus",
            TemplateValueGatherTypeVendors.GetDisplayName(TemplateValueGatherType.StoraxeModbus, _stringHelper));
    }

    [Fact]
    public void EveryDeviceCanBeToldApartByItsName()
    {
        //Two entries with the same name in the search list would look like the same product offered twice.
        var names = Enum.GetValues<TemplateValueGatherType>()
            .Select(t => TemplateValueGatherTypeVendors.GetDisplayName(t, _stringHelper))
            .ToList();

        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AModelUnderItsMakeIsNotPrefixedWithTheMakeAgain()
    {
        Assert.Equal("Hybrid Inverter Modbus",
            TemplateValueGatherTypeVendors.GetModelName(TemplateValueGatherType.SmaHybridInverterModbus, _stringHelper));
    }

    [Fact]
    public void AModelNamedWithoutItsMakeKeepsItsWholeName()
    {
        Assert.Equal("Storaxe Modbus",
            TemplateValueGatherTypeVendors.GetModelName(TemplateValueGatherType.StoraxeModbus, _stringHelper));
    }

    [Fact]
    public void TheDisplayNameIsTheMakeFollowedByTheModel()
    {
        Assert.All(Enum.GetValues<TemplateValueGatherType>(), type => Assert.Equal(
            $"{TemplateValueGatherTypeVendors.GetVendor(type)} {TemplateValueGatherTypeVendors.GetModelName(type, _stringHelper)}",
            TemplateValueGatherTypeVendors.GetDisplayName(type, _stringHelper)));
    }

    [Fact]
    public void EveryBrandIsOfferedOnceAndInAlphabeticalOrder()
    {
        var vendors = TemplateValueGatherTypeVendors.GetVendors();

        Assert.Equal(vendors.Distinct().Count(), vendors.Count);
        Assert.Equal(vendors.OrderBy(v => v, StringComparer.OrdinalIgnoreCase), vendors);
        Assert.Contains("SMA", vendors);
        Assert.Contains("Fronius", vendors);
    }

    [Fact]
    public void EveryDeviceIsOfferedUnderExactlyOneBrand()
    {
        //A device missing here could not be chosen at all, and one listed twice would look like two products.
        var offered = TemplateValueGatherTypeVendors.GetVendors()
            .SelectMany(TemplateValueGatherTypeVendors.GetTypesOf)
            .ToList();

        Assert.Equal(offered.Distinct().Count(), offered.Count);
        Assert.Equal(Enum.GetValues<TemplateValueGatherType>().OrderBy(t => t), offered.OrderBy(t => t));
    }

    [Fact]
    public void ABrandOffersOnlyItsOwnDevices()
    {
        var smaDevices = TemplateValueGatherTypeVendors.GetTypesOf("SMA");

        Assert.Equal(7, smaDevices.Count);
        Assert.Contains(TemplateValueGatherType.SmaHybridInverterModbus, smaDevices);
        Assert.All(smaDevices, type => Assert.Equal("SMA", TemplateValueGatherTypeVendors.GetVendor(type)));
    }

    [Fact]
    public void ABrandWithASingleDeviceOffersOnlyThatOne()
    {
        Assert.Equal(new[] { TemplateValueGatherType.TeslaPowerwallFleetApi, }, TemplateValueGatherTypeVendors.GetTypesOf("Tesla"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown brand")]
    public void NoOrAnUnknownBrandOffersNoDevices(string? vendor)
    {
        Assert.Empty(TemplateValueGatherTypeVendors.GetTypesOf(vendor));
    }

    [Fact]
    public void TheDevicesOfOneBrandCanBeToldApartByTheirModelName()
    {
        //Under a chosen brand only the model is shown, so the model alone has to tell the devices apart.
        Assert.All(TemplateValueGatherTypeVendors.GetVendors(), vendor =>
        {
            var modelNames = TemplateValueGatherTypeVendors.GetTypesOf(vendor)
                .Select(type => TemplateValueGatherTypeVendors.GetModelName(type, _stringHelper))
                .ToList();
            Assert.All(modelNames, name => Assert.False(string.IsNullOrWhiteSpace(name)));
            Assert.Equal(modelNames.Count, modelNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        });
    }

    [Fact]
    public void TheFirstDeviceOfAKindIsNamedAfterIt()
    {
        var name = TemplateValueGatherTypeVendors.SuggestName(TemplateValueGatherType.SmaHybridInverterModbus,
            Array.Empty<string?>(), _stringHelper);

        Assert.Equal("SMA Hybrid Inverter Modbus", name);
    }

    [Fact]
    public void ASecondDeviceOfTheSameKindIsNumbered()
    {
        var name = TemplateValueGatherTypeVendors.SuggestName(TemplateValueGatherType.SmaHybridInverterModbus,
            new[] { "SMA Hybrid Inverter Modbus", }, _stringHelper);

        Assert.Equal("SMA Hybrid Inverter Modbus 2", name);
    }

    [Fact]
    public void NumberingSkipsNumbersAlreadyInUse()
    {
        var name = TemplateValueGatherTypeVendors.SuggestName(TemplateValueGatherType.SmaHybridInverterModbus,
            new[] { "SMA Hybrid Inverter Modbus", "SMA Hybrid Inverter Modbus 2", }, _stringHelper);

        Assert.Equal("SMA Hybrid Inverter Modbus 3", name);
    }

    [Fact]
    public void ANameDifferingOnlyInCaseOrSpacingCountsAsTaken()
    {
        var name = TemplateValueGatherTypeVendors.SuggestName(TemplateValueGatherType.SmaHybridInverterModbus,
            new[] { "  sma hybrid inverter modbus ", }, _stringHelper);

        Assert.Equal("SMA Hybrid Inverter Modbus 2", name);
    }

    [Fact]
    public void UnnamedAndUnrelatedDevicesDoNotCauseNumbering()
    {
        var name = TemplateValueGatherTypeVendors.SuggestName(TemplateValueGatherType.SmaHybridInverterModbus,
            new[] { null, "", "   ", "SMA Hybrid Inverter Modbus garage", "Fronius Gen24", }, _stringHelper);

        Assert.Equal("SMA Hybrid Inverter Modbus", name);
    }
}
