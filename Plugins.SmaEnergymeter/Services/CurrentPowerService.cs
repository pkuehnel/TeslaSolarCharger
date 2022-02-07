namespace Plugins.SmaEnergymeter.Services;

public class CurrentPowerService
{
    private readonly ILogger<CurrentPowerService> _logger;
    private readonly SharedValues _sharedValues;

    public CurrentPowerService(ILogger<CurrentPowerService> logger, SharedValues sharedValues)
    {
        _logger = logger;
        _sharedValues = sharedValues;
    }

    public int GetCurrentPower(int lastXSeconds)
    {
        _logger.LogTrace("{method}({param1})", nameof(GetCurrentPower), lastXSeconds);
        var orderedValues = _sharedValues.LastValues
            .Where(v => v.Timestamp >= DateTime.UtcNow.AddSeconds(-lastXSeconds))
            .OrderBy(v => v.Timestamp)
            .ToList();

        long weightedSum = 0;
        for (var i = 0; i < orderedValues.Count; i++)
        {
            weightedSum += orderedValues[i].Power * (i + 1);
            _logger.LogTrace("weightedSum: {value}", weightedSum);
        }
        var weightedCount = orderedValues.Count * (orderedValues.Count + 1) / 2;
        if (weightedCount == 0)
        {
            throw new InvalidOperationException("There are no power values available");
        }
        return (int) (weightedSum / weightedCount);
    }
}