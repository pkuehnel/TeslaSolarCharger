using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueEstimationService(ILogger<MeterValueEstimationService> logger,
    ITeslaSolarChargerContext context,
    ITscConfigurationService tscConfigurationService,
    IConstants constants) : IMeterValueEstimationService
{
    // The following method is required as up to v 2.36.4 the values inserted to the database had null as estimated value.
    public async Task FillMissingEstimatedMeterValuesInDatabase()
    {
        logger.LogTrace("{method}()", nameof(FillMissingEstimatedMeterValuesInDatabase));
        var valuesAlreadyUpdated = await tscConfigurationService.GetConfigurationValueByKey(constants.MeterValueEstimatesCreated);
        const string alreadyUpdatedValue = "true";
        if (string.Equals(valuesAlreadyUpdated, alreadyUpdatedValue))
        {
            logger.LogDebug("Meter values already updated, skipping.");
            return;
        }
        var meterValuesQuery = context.MeterValues
            .Where(v => v.EstimatedEnergyWs == null)
            .AsQueryable();
        var meterValuesToUpdateGroups = await meterValuesQuery
            .OrderBy(v => v.Timestamp)
            .GroupBy(m => m.MeterValueKind)
            .ToListAsync();
        foreach (var group in meterValuesToUpdateGroups)
        {
            if (!group.Any())
            {
                continue;
            }

            MeterValue? latestKnownValue = null;
            //As before that date no values were saved, they must be invalid and therefore ignored
            var minimumTimeStamp = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);
            foreach (var meterValue in group)
            {
                if (meterValue.Timestamp < minimumTimeStamp)
                {
                    context.MeterValues.Remove(meterValue);
                    continue;
                }
                latestKnownValue = await UpdateMeterValueEstimation(meterValue, latestKnownValue).ConfigureAwait(false);
            }

            await context.SaveChangesAsync();
        }
        await tscConfigurationService.SetConfigurationValueByKey(constants.MeterValueEstimatesCreated, alreadyUpdatedValue);
    }

    /// <summary>
    /// Update the meter value estimations based on the latest known value.
    /// </summary>
    /// <param name="meterValue">Meter values whose values should be estimated</param>
    /// <param name="latestKnownValue">latest known value of the same kind like meter value. Calling with null is not recommended as this results in a call to the database and dramatically slows down method execution time</param>
    /// <returns>New latest known value that can be used for future method calls as baseline</returns>
    /// <exception cref="InvalidDataException">Thrown when latest known value and meter value do not have the same kind.</exception>
    public async Task<MeterValue> UpdateMeterValueEstimation(MeterValue meterValue, MeterValue? latestKnownValue)
    {
        logger.LogTrace("{method}({meterValue}, {latestKnownValue})", nameof(UpdateMeterValueEstimation), meterValue, latestKnownValue);
        if (latestKnownValue == default)
        {
            logger.LogTrace("No latest known value provided, fetching from database for {@meterValue}", meterValue);
            latestKnownValue = await context.MeterValues
                .Where(v => v.MeterValueKind == meterValue.MeterValueKind
                            && v.CarId == meterValue.CarId
                            && v.EstimatedEnergyWs != null)
                .OrderByDescending(v => v.Id)
                .AsNoTracking()
                .FirstOrDefaultAsync() ?? new MeterValue(meterValue.Timestamp, meterValue.MeterValueKind, 0)
                {
                    Id = 0,
                    MeasuredHomeBatteryPower = 0,
                    MeasuredGridPower = 0,
                    EstimatedEnergyWs = 0,
                    EstimatedHomeBatteryEnergyWs = 0,
                    EstimatedGridEnergyWs = 0,
                };

            latestKnownValue.EstimatedGridEnergyWs ??= 0;
            latestKnownValue.EstimatedHomeBatteryEnergyWs ??= 0;
        }
        else
        {
            if (meterValue.MeterValueKind != latestKnownValue.MeterValueKind)
            {
                throw new InvalidDataException(
                    $"The meter value {meterValue.Id} has a different MeterValueKind than the last known value {latestKnownValue.Id}");
            }
        }

        // Calculate the time difference (in hours) between the current record and the previous known value.
        // Note: Ensure that Timestamp is set properly for both values.
        var elapsedSeconds = (meterValue.Timestamp - latestKnownValue.Timestamp).TotalSeconds;
        if (elapsedSeconds < 0)
        {
            // Handle edge cases like identical timestamps.
            logger.LogWarning("The timestamp of the meter value {@meterValue} is not newer than the last meterValue {@lastMeterValueId}", meterValue, latestKnownValue);
            throw new InvalidDataException(
                $"The timestamp of the meter value {meterValue.Id} is not newer than the last meterValue {latestKnownValue.Id}");
        }

        meterValue.EstimatedEnergyWs =
            CalculateEstimatedEnergy(latestKnownValue.EstimatedEnergyWs, meterValue.MeasuredPower, elapsedSeconds);
        meterValue.EstimatedGridEnergyWs =
            CalculateEstimatedEnergy(latestKnownValue.EstimatedGridEnergyWs, meterValue.MeasuredGridPower, elapsedSeconds);
        meterValue.EstimatedHomeBatteryEnergyWs =
            CalculateEstimatedEnergy(latestKnownValue.EstimatedHomeBatteryEnergyWs, meterValue.MeasuredHomeBatteryPower, elapsedSeconds);

        latestKnownValue = meterValue;

        return latestKnownValue;
    }

    private long? CalculateEstimatedEnergy(long? lastKnownEstimatedEnergy, int measuredPower, double elapsedSeconds)
    {
        if (lastKnownEstimatedEnergy == default)
        {
            return default;
        }
        var energySinceLastValue = (int)(measuredPower * elapsedSeconds);
        return lastKnownEstimatedEnergy.Value + energySinceLastValue;
    }
}
