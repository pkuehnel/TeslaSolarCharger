namespace TeslaSolarCharger.Server.Services;

public class ChargingStationService
{
    private readonly ILogger<ChargingStationService> _logger;

    public ChargingStationService(ILogger<ChargingStationService> logger)
    {
        _logger = logger;
    }


}
