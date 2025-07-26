using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueLogService(ILogger<MeterValueLogService> logger,
    ITeslaSolarChargerContext context,
    IIndexService indexService,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    IMeterValueBufferService meterValueBufferService,
    IMeterValueEstimationService meterValueEstimationService) : IMeterValueLogService
{
    public async Task AddPvValuesToBuffer()
    {
        logger.LogTrace("{method}()", nameof(AddPvValuesToBuffer));
        var pvValues = await indexService.GetPvValues().ConfigureAwait(false);
        if(pvValues.LastUpdated == default)
        {
            logger.LogWarning("Unknown last updated of PV values, do not log pv meter values");
            return;
        }
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var solarRefreshRate = configurationWrapper.PvValueJobUpdateIntervall();
        var minimumPvValueTimeStamp = currentDate - (2 * solarRefreshRate);
        if (pvValues.LastUpdated.Value <= minimumPvValueTimeStamp)
        {
            logger.LogWarning("Pv Values are too old, do not log");
            return;
        }
        if (pvValues.InverterPower == default)
        {
            logger.LogInformation("Unknown inverter power, do not log pv meter values");
            return;
        }
        var solarMeterValue = new MeterValue
        {
            Timestamp = pvValues.LastUpdated.Value,
            MeterValueKind = MeterValueKind.SolarGeneration,
            MeasuredPower = pvValues.InverterPower,
            MeasuredEnergyWs = null,
        };
        meterValueBufferService.Add(solarMeterValue);
        var homeBatteryPower = pvValues.HomeBatteryPower ?? 0;
        var chargingPower = pvValues.CarCombinedChargingPowerAtHome ?? 0;
        var homePower = pvValues.InverterPower - pvValues.GridPower - homeBatteryPower - chargingPower;
        if (homePower == default)
        {
            logger.LogInformation("Unknown home power, do not log pv meter values");
            return;
        }
        var houseMeterValue = new MeterValue
        {
            Timestamp = pvValues.LastUpdated.Value,
            MeterValueKind = MeterValueKind.HouseConsumption,
            MeasuredPower = homePower,
            MeasuredEnergyWs = null,
        };
        meterValueBufferService.Add(houseMeterValue);
    }

    public async Task SaveBufferedMeterValuesToDatabase()
    {
        logger.LogTrace("{method}()", nameof(SaveBufferedMeterValuesToDatabase));
        var meterValues = meterValueBufferService.DrainAll();
        var meterValueGroups = meterValues.GroupBy(m => m.MeterValueKind);
        foreach (var meterValueGroup in meterValueGroups)
        {
            var elements = meterValueGroup.OrderBy(m => m.Timestamp).ToList();
            MeterValue? latestKnownElement = null;
            foreach (var element in elements)
            {
                latestKnownElement = await meterValueEstimationService.UpdateMeterValueEstimation(element, latestKnownElement).ConfigureAwait(false);
                context.MeterValues.Add(element);
            }

        }
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
