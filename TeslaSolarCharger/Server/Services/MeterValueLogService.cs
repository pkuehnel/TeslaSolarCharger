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
    IDatabaseValueBufferService databaseValueBufferService,
    IMeterValueEstimationService meterValueEstimationService,
    ISettings settings) : IMeterValueLogService
{
    public async Task AddPvValuesToBuffer()
    {
        logger.LogTrace("{method}()", nameof(AddPvValuesToBuffer));
        var pvValues = await indexService.GetPvValues().ConfigureAwait(false);
        if (pvValues.LastUpdated == default)
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
            databaseValueBufferService.Add(meterValue);
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
            databaseValueBufferService.Add(meterValue);
        }
        if (pvValues.HomeBatteryPower != default)
        {
            var isDischarging = pvValues.HomeBatteryPower < 0;
            var chargingValue = new MeterValue
            {
                Timestamp = pvValues.LastUpdated.Value,
                MeterValueKind = MeterValueKind.HomeBatteryCharging,
                MeasuredPower = isDischarging ? 0 : pvValues.HomeBatteryPower.Value,
                MeasuredEnergyWs = null,
            };
            databaseValueBufferService.Add(chargingValue);
            var dischargingValue = new MeterValue
            {
                Timestamp = pvValues.LastUpdated.Value,
                MeterValueKind = MeterValueKind.HomeBatteryDischarging,
                MeasuredPower = isDischarging ? (-pvValues.HomeBatteryPower.Value) : 0,
                MeasuredEnergyWs = null,
            };
            databaseValueBufferService.Add(dischargingValue);
        }

        if (pvValues.GridPower != default)
        {
            var isPowerComingFromGrid = pvValues.GridPower < 0;
            var powerToGrid = new MeterValue()
            {
                Timestamp = pvValues.LastUpdated.Value,
                MeterValueKind = MeterValueKind.PowerToGrid,
                MeasuredPower = isPowerComingFromGrid ? 0 : pvValues.GridPower.Value,
                MeasuredEnergyWs = null,
            };
            databaseValueBufferService.Add(powerToGrid);
            var powerFromGrid = new MeterValue()
            {
                Timestamp = pvValues.LastUpdated.Value,
                MeterValueKind = MeterValueKind.PowerFromGrid,
                MeasuredPower = isPowerComingFromGrid ? (-pvValues.GridPower.Value) : 0,
                MeasuredEnergyWs = null,
            };
            databaseValueBufferService.Add(powerFromGrid);
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
        var stopWatch = System.Diagnostics.Stopwatch.StartNew();
        var meterValues = databaseValueBufferService.DrainAll<MeterValue>();
        var meterValueGroups = meterValues.GroupBy(m => new {m.MeterValueKind, m.CarId,});
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
        stopWatch.Stop();
        logger.LogInformation("Saved {count} meter values to database in {elapsedMilliseconds} ms", meterValues.Count, stopWatch.ElapsedMilliseconds);
    }
}
