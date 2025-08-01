using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueLogService(ILogger<MeterValueLogService> logger,
    ITeslaSolarChargerContext context,
    IIndexService indexService,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    IMeterValueBufferService meterValueBufferService,
    IMeterValueEstimationService meterValueEstimationService,
    ISettings settings) : IMeterValueLogService
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
        if (pvValues.InverterPower != default)
        {
            var meterValue = new MeterValue
            {
                Timestamp = pvValues.LastUpdated.Value,
                MeterValueKind = MeterValueKind.SolarGeneration,
                MeasuredPower = pvValues.InverterPower,
                MeasuredEnergyWs = null,
            };
            meterValueBufferService.Add(meterValue);
        }
        var homeBatteryPower = pvValues.HomeBatteryPower ?? 0;
        var chargingPower = pvValues.CarCombinedChargingPowerAtHome ?? 0;
        var homePower = pvValues.InverterPower - pvValues.GridPower - homeBatteryPower - chargingPower;
        if (homePower != default)
        {
            var meterValue = new MeterValue
            {
                Timestamp = pvValues.LastUpdated.Value,
                MeterValueKind = MeterValueKind.HouseConsumption,
                MeasuredPower = homePower,
                MeasuredEnergyWs = null,
            };
            meterValueBufferService.Add(meterValue);
        }
        if (pvValues.HomeBatteryPower != default)
        {
            var meterValue = new MeterValue
            {
                Timestamp = pvValues.LastUpdated.Value,
                MeterValueKind = pvValues.HomeBatteryPower < 0 ? MeterValueKind.HomeBatteryDischarging : MeterValueKind.HomeBatteryCharging,
                MeasuredPower = Math.Abs(pvValues.HomeBatteryPower.Value),
                MeasuredEnergyWs = null,
            };
            meterValueBufferService.Add(meterValue);
        }

        if (pvValues.HomeBatterySoc != default
            && pvValues.HomeBatterySoc != settings.LastLoggedHomeBatterySoc)
        {
            var pvValueLog = new PvValueLog()
            {
                Timestamp = pvValues.LastUpdated.Value,
                Type = PvValueType.HomeBatterySoc,
                IntValue = pvValues.HomeBatterySoc.Value,
            };
            context.PvValueLogs.Add(pvValueLog);
            await context.SaveChangesAsync().ConfigureAwait(false);
            settings.LastLoggedHomeBatterySoc = pvValueLog.IntValue;
        }
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
