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
