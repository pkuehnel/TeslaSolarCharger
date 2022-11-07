using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Helper;

public class NodePatternTypeHelper : INodePatternTypeHelper
{
    private readonly ILogger<NodePatternTypeHelper> _logger;

    public NodePatternTypeHelper(ILogger<NodePatternTypeHelper> logger)
    {
        _logger = logger;
    }

    public NodePatternType DecideNodePatternType(string? jsonPattern, string? xmlPattern)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(DecideNodePatternType), jsonPattern, xmlPattern);
        NodePatternType nodePatternType;
        if (!string.IsNullOrWhiteSpace(jsonPattern))
        {
            nodePatternType = NodePatternType.Json;
        }
        else if (!string.IsNullOrWhiteSpace(xmlPattern))
        {
            nodePatternType = NodePatternType.Xml;
        }
        else
        {
            nodePatternType = NodePatternType.Direct;
        }
        _logger.LogTrace("Node pattern type is {nodePatternType}", nodePatternType);
        return nodePatternType;
    }
}
