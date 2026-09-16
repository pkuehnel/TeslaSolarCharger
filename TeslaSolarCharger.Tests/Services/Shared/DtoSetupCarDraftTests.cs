using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Setup;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

/// <summary>
/// What a car in setup is called on screen, before and after the user has named it, and when it may get a row.
/// </summary>
public class DtoSetupCarDraftTests
{
    private static DtoSetupCarDraft Draft(string? name, string? make) => new()
    {
        Make = make,
        Configuration = new CarBasicConfiguration { Name = name, },
    };

    [Fact]
    public void ANamedCarIsCalledByItsName()
    {
        Assert.Equal("Family car", Draft("Family car", "Tesla").GetDisplayName());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ACarWithoutANameIsCalledByItsMake(string? name)
    {
        Assert.Equal("Hyundai", Draft(name, "  Hyundai ").GetDisplayName());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", "  ")]
    public void ACarWithNeitherHasNoNameYet(string? name, string? make)
    {
        //Left to the screen, which says "unnamed car" in the user's language.
        Assert.Null(Draft(name, make).GetDisplayName());
    }

    [Fact]
    public void ACarWithANameAndAVinCanBeStored()
    {
        var draft = new DtoSetupCarDraft { Configuration = new CarBasicConfiguration { Name = "Car", Vin = "VIN1", }, };

        Assert.True(draft.CanBeStored());
    }

    [Theory]
    [InlineData(null, "VIN1")]
    [InlineData("Car", "")]
    [InlineData("  ", "VIN1")]
    [InlineData("Car", "   ")]
    [InlineData(null, "")]
    public void ANewCarThatCannotBeToldApartCannotBeStored(string? name, string vin)
    {
        var draft = new DtoSetupCarDraft { Configuration = new CarBasicConfiguration { Name = name, Vin = vin, }, };

        Assert.False(draft.CanBeStored());
    }

    [Fact]
    public void ACarThatAlreadyHasARowCanAlwaysBeStored()
    {
        //Its row exists already, so saving updates it rather than creating a nameless one.
        var draft = new DtoSetupCarDraft { CarId = 4, Configuration = new CarBasicConfiguration { Vin = string.Empty, }, };

        Assert.True(draft.CanBeStored());
    }
}
