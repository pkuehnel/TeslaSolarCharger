using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources;

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
        MeterValue? inverterValue = null;
        MeterValue? toGridValue = null;
        MeterValue? fromGridValue = null;
        MeterValue? homeBatteryChargingValue = null;
        MeterValue? homeBatteryDischargingValue = null;
        MeterValue? houseConsumptionValue = null;
        if (pvValues.InverterPower != default)
        {
            inverterValue =
                new MeterValue(pvValues.LastUpdated.Value,
                    MeterValueKind.SolarGeneration,
                    pvValues.InverterPower.Value);
        }
        var homeBatteryPower = pvValues.HomeBatteryPower ?? 0;
        var chargingPower = pvValues.CarCombinedChargingPowerAtHome ?? 0;
        var homePower = pvValues.InverterPower - pvValues.GridPower - homeBatteryPower - chargingPower;
        if (homePower != default)
        {
            houseConsumptionValue =
                new MeterValue(pvValues.LastUpdated.Value,
                    MeterValueKind.HouseConsumption,
                    homePower.Value);
        }
        if (pvValues.HomeBatteryPower != default)
        {
            var isDischarging = pvValues.HomeBatteryPower < 0;
            homeBatteryChargingValue =
                new MeterValue(pvValues.LastUpdated.Value,
                    MeterValueKind.HomeBatteryCharging,
                    isDischarging ? 0 : pvValues.HomeBatteryPower.Value);
            homeBatteryDischargingValue =
                new MeterValue(pvValues.LastUpdated.Value,
                    MeterValueKind.HomeBatteryDischarging,
                    isDischarging ? (-pvValues.HomeBatteryPower.Value) : 0);
        }

        if (pvValues.GridPower != default)
        {
            var isPowerComingFromGrid = pvValues.GridPower < 0;
            toGridValue =
                new MeterValue(pvValues.LastUpdated.Value,
                    MeterValueKind.PowerToGrid,
                    isPowerComingFromGrid ? 0 : pvValues.GridPower.Value);
            fromGridValue =
                new MeterValue(pvValues.LastUpdated.Value,
                    MeterValueKind.PowerFromGrid,
                    isPowerComingFromGrid ? (-pvValues.GridPower.Value) : 0);
        }

        AddHomeBatteryAndGridPowers(pvValues, fromGridValue, toGridValue, homeBatteryDischargingValue, homeBatteryChargingValue, houseConsumptionValue);


        if (inverterValue != null)
        {
            databaseValueBufferService.Add(inverterValue);
        }
        if (toGridValue != null)
        {
            databaseValueBufferService.Add(toGridValue);
        }
        if (fromGridValue != null)
        {
            databaseValueBufferService.Add(fromGridValue);
        }
        if (homeBatteryChargingValue != null)
        {
            databaseValueBufferService.Add(homeBatteryChargingValue);
        }
        if (homeBatteryDischargingValue != null)
        {
            databaseValueBufferService.Add(homeBatteryDischargingValue);
        }
        if (houseConsumptionValue != null)
        {
            databaseValueBufferService.Add(houseConsumptionValue);
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

    internal void AddHomeBatteryAndGridPowers(DtoPvValues pvValues, MeterValue? fromGridValue, MeterValue? toGridValue,
        MeterValue? homeBatteryDischargingValue, MeterValue? homeBatteryChargingValue, MeterValue? houseConsumptionValue)
    {
        var powerFromGrid = fromGridValue?.MeasuredPower ?? 0;
        var carsChargingPower = (pvValues.CarCombinedChargingPowerAtHome ?? 0);
        var relevantPowerFromGrid = Math.Max(0, powerFromGrid - carsChargingPower);
        carsChargingPower = Math.Max(0, carsChargingPower - relevantPowerFromGrid);
        var homeBatteryDischargingPower = homeBatteryDischargingValue?.MeasuredPower ?? 0;
        var relevantHomeBatteryDischargingPower = Math.Max(0, homeBatteryDischargingPower - carsChargingPower);
        if (relevantPowerFromGrid > 0 && (homeBatteryChargingValue?.MeasuredPower > 0))
        {
            homeBatteryChargingValue.MeasuredGridPower = Math.Min(homeBatteryChargingValue.MeasuredPower, relevantPowerFromGrid);
            relevantPowerFromGrid -= homeBatteryChargingValue.MeasuredGridPower;
        }
        if (relevantHomeBatteryDischargingPower > 0 && (toGridValue?.MeasuredPower > 0))
        {
            toGridValue.MeasuredHomeBatteryPower = Math.Min(toGridValue.MeasuredPower, relevantHomeBatteryDischargingPower);
            relevantHomeBatteryDischargingPower -= toGridValue.MeasuredHomeBatteryPower;
        }
        if (relevantPowerFromGrid > 0 && houseConsumptionValue != default)
        {
            houseConsumptionValue.MeasuredGridPower = relevantPowerFromGrid;
        }
        if (relevantHomeBatteryDischargingPower > 0 && houseConsumptionValue != default)
        {
            houseConsumptionValue.MeasuredHomeBatteryPower = relevantHomeBatteryDischargingPower;
        }
    }

    public async Task SaveBufferedMeterValuesToDatabase(bool shutsdownAfterSave)
    {
        logger.LogInformation("{method}()", nameof(SaveBufferedMeterValuesToDatabase));
        var stopWatch = System.Diagnostics.Stopwatch.StartNew();
        var meterValues = databaseValueBufferService.DrainAll<MeterValue>();
        //Order to improce performance of the database insert
        meterValues = meterValues.OrderBy(m => m.CarId)
            .ThenBy(m => m.MeterValueKind)
            .ThenBy(m => m.Timestamp)
            .ToList();
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
        logger.LogInformation("Saving {count} meter values to database after {elapsedMs} ms", meterValues.Count, stopWatch.ElapsedMilliseconds);
        await context.SaveChangesAsync().ConfigureAwait(false);
        if (!shutsdownAfterSave)
        {
            logger.LogInformation("Recreate index on meter values to improve query performance after {elapsedMs} ms", stopWatch.ElapsedMilliseconds);
            await context.Database.ExecuteSqlRawAsync(
                $"CREATE INDEX IF NOT EXISTS {StaticConstants.MeterValueIndexName} ON MeterValues({nameof(MeterValue.CarId)}, {nameof(MeterValue.MeterValueKind)}, {nameof(MeterValue.Timestamp)})");
        }
        stopWatch.Stop();
        logger.LogInformation("Saved {count} meter values to database in {elapsedMilliseconds} ms", meterValues.Count, stopWatch.ElapsedMilliseconds);
    }
}
