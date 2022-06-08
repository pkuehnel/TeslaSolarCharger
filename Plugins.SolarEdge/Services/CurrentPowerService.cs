namespace Plugins.SolarEdge.Services;

public class CurrentPowerService
{
    private readonly ILogger<CurrentPowerService> _logger;
    private readonly CurrentPowerService _currentPowerService;

    public CurrentPowerService(ILogger<CurrentPowerService> logger, CurrentPowerService currentPowerService)
    {
        _logger = logger;
        _currentPowerService = currentPowerService;
    }
}