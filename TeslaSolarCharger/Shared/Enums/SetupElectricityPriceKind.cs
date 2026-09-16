namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// The shape of the user's electricity contract, asked in the words of a bill rather than of a tariff model. Which
/// fields are worth showing follows from this, so only the matching ones are ever put on screen.
/// </summary>
public enum SetupElectricityPriceKind
{
    /// <summary>One price, all day, every day.</summary>
    Fixed = 0,

    /// <summary>Different prices at set times, for example a cheaper night rate.</summary>
    TimeOfUse = 1,

    /// <summary>A price that follows the market, with the contract's own markup on top.</summary>
    Market = 2,
}
