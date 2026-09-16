using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Setup;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

/// <summary>
/// What a car in setup is called on screen, before and after the user has named it.
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
}
