using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueLogService(ILogger<MeterValueLogService> logger,
    ITeslaSolarChargerContext context,
    IIndexService indexService) : IMeterValueLogService
{
    public async Task LogPvValues()
    {
        logger.LogTrace("{method}()", nameof(LogPvValues));
        var pvValues = indexService.GetPvValues();
        if(pvValues.LastUpdated == default)
        {
            logger.LogWarning("Unknown last updated of PV values, do not log pv meter values");
            return;
        }
        if (pvValues.InverterPower == default)
        {
            logger.LogInformation("Unknown inverter power, do not log pv meter values");
            return;
        }
        await LogValue(pvValues.LastUpdated.Value, MeterValueKind.SolarGeneration, pvValues.InverterPower, null).ConfigureAwait(false);
        var homeBatteryPower = pvValues.HomeBatteryPower ?? 0;
        var chargingPower = pvValues.CarCombinedChargingPowerAtHome ?? 0;
        var homePower = pvValues.InverterPower - pvValues.GridPower - homeBatteryPower - chargingPower;
        if (homePower == default)
        {
            logger.LogInformation("Unknown home power, do not log pv meter values");
            return;
        }
        await LogValue(pvValues.LastUpdated.Value, MeterValueKind.HouseConsumption, homePower, null).ConfigureAwait(false);
    }

    private async Task LogValue(DateTimeOffset timestamp, MeterValueKind meterValueKind, int? measuredPower, int? measuredEnergy)
    {
        logger.LogTrace("{method}({timestamp}, {meterValueKind}, {measuredPower}, {measuredEnergy})",
            nameof(LogValue), timestamp, meterValueKind, measuredPower, measuredEnergy);
        var meterDatum = new MeterValue
        {
            Timestamp = timestamp,
            MeterValueKind = meterValueKind,
            MeasuredPower = measuredPower,
            MeasuredEnergy = measuredEnergy,
        };
        context.MeterValues.Add(meterDatum);
        await context.SaveChangesAsync();
    }
}
