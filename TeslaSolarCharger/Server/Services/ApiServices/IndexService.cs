using System.Configuration;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

namespace TeslaSolarCharger.Server.Services.ApiServices;

public class IndexService : IIndexService
{
    private readonly ILogger<IndexService> _logger;
    private readonly ISettings _settings;

    public IndexService(ILogger<IndexService> logger, ISettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public DtoPvValues GetPvValues()
    {
        _logger.LogTrace("{method}()", nameof(GetPvValues));
        return new DtoPvValues()
        {
            GridPower = _settings.Overage,
            InverterPower = _settings.InverterPower,
            HomeBatteryPower = _settings.HomeBatteryPower,
            HomeBatterySoc = _settings.HomeBatterySoc,
            CarCombinedChargingPowerAtHome = _settings.Cars.Select(c => c.CarState.ChargingPowerAtHome).Sum(),
        };
    }
}
