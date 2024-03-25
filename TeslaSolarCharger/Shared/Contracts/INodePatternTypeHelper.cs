using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Contracts;

public interface INodePatternTypeHelper
{
    NodePatternType DecideNodePatternType(string? jsonPattern, string? xmlPattern);
}
