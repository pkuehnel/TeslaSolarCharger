using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IValidFromToSplitter
{
    /// <summary>
    /// Overload: returns segments inside [startDate, endDate] split by boundaries,
    /// PLUS all values outside the boundaries (unchanged except where they cross a boundary,
    /// in which case they are cut exactly at the boundary).
    /// </summary>
    (List<TLeft> SplitLeft, List<TRight> SplitRight) SplitByBoundaries<TLeft, TRight>(
        List<TLeft> leftItems,
        List<TRight> rightItems,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        bool includeOuterSegments)
        where TLeft : ValidFromToBase, new()
        where TRight : ValidFromToBase, new();
}
