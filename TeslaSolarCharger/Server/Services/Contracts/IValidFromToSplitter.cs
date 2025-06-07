using TeslaSolarCharger.Server.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IValidFromToSplitter
{
    (
        List<TLeft> SplitLeft, List<TRight> SplitRight) SplitByBoundaries<TLeft, TRight>(List<TLeft> leftItems, List<TRight> rightItems, DateTimeOffset startDate, DateTimeOffset endDate)
        where TLeft : ValidFromToBase, new()
        where TRight : ValidFromToBase, new();
}
