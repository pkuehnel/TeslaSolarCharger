using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
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
    ILoadPointManagementService loadPointManagementService,
    IGenericValueService genericValueService)
    : IIndexService
{
    public async Task<DtoPvValues> GetPvValues()
    {
        logger.LogTrace("{method}()", nameof(GetPvValues));
        var sourceValues = genericValueService.GetSourceValues(true);
        if (configurationWrapper.ShouldUseFakeSolarValues())
        {
            //Fake values replace every real device instead of adding to it.
            sourceValues = sourceValues.Where(v => v.ConfigurationType == ConfigurationType.FakeSolarValue).ToList();
        }
        var loadPoints = await loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var pvValues = new DtoPvValues()
        {
            SourceValues = sourceValues,
            CarCombinedChargingPowerAtHome = loadPoints.Select(l => l.ChargingPower).Sum(),
            LastUpdated = settings.LastPvValueUpdate,
        };
        //Without solar or grid values there is nothing the buffer could be taken from.
        pvValues.PowerBuffer = pvValues.InverterPower == null && pvValues.GridPower == null
            ? null
            : configurationWrapper.PowerBuffer();
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

