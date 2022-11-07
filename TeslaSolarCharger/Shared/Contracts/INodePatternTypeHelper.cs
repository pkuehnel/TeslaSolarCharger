using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Contracts;

public interface INodePatternTypeHelper
{
    NodePatternType DecideNodePatternType(string? jsonPattern, string? xmlPattern);
}
