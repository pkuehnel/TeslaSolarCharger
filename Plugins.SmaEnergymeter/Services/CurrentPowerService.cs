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

    public int GetCurrentPower()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentPower));
        return _sharedValues.OverageW;
    }

    public SharedValues GetAllValues()
    {
        _logger.LogTrace("{method}()", nameof(GetAllValues));
        return _sharedValues;
    }
}
