using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.ApiServices;

public class IndexService(
    ILogger<IndexService> logger,
    ISettings settings,
    IConfigurationWrapper configurationWrapper,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ILoadPointManagementService loadPointManagementService)
    : IIndexService
{
    public async Task<DtoPvValues> GetPvValues()
    {
        logger.LogTrace("{method}()", nameof(GetPvValues));
        int? powerBuffer = configurationWrapper.PowerBuffer();
        if (settings.InverterPower == null && settings.Overage == null)
        {
            powerBuffer = null;
        }
        var loadPoints = await loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var pvValues = new DtoPvValues()
        {
            GridPower = settings.Overage,
            InverterPower = settings.InverterPower,
            HomeBatteryPower = settings.HomeBatteryPower,
            HomeBatterySoc = settings.HomeBatterySoc,
            PowerBuffer = powerBuffer,
            CarCombinedChargingPowerAtHome = loadPoints.Select(l => l.ChargingPower).Sum(),
            LastUpdated = settings.LastPvValueUpdate,
        };
        return pvValues;
    }

    public async Task UpdateCarFleetApiState(int carId, TeslaCarFleetApiState fleetApiState)
    {
        logger.LogTrace("{method}({carId}, {fleetApiState})", nameof(UpdateCarFleetApiState), carId, fleetApiState);
        var car = teslaSolarChargerContext.Cars.First(c => c.Id == carId);
        car.TeslaFleetApiState = fleetApiState;
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }
}

