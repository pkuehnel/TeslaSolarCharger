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
            latestKnownValue = await context.MeterValues
                .Where(v => v.MeterValueKind == meterValue.MeterValueKind && v.EstimatedEnergyWs != null)
                .OrderByDescending(v => v.Id)
                .AsNoTracking()
                .FirstOrDefaultAsync() ?? new MeterValue()
            {
                Id = 0,
                Timestamp = meterValue.Timestamp,
                MeterValueKind = meterValue.MeterValueKind,
                EstimatedEnergyWs = 0,
                EstimatedPower = 0,
                MeasuredEnergyWs = 0,
                MeasuredPower = 0,
            };
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

        // Fill in EstimatedEnergy if MeasuredEnergy is missing.
        if (!meterValue.MeasuredEnergyWs.HasValue)
        {
            // Use the current MeasuredPower if available; otherwise fallback to the previous known MeasuredPower.
            var power = meterValue.MeasuredPower ?? latestKnownValue.MeasuredPower ?? 0;
            var energySinceLastValue = (int)(power * elapsedSeconds);
            if (latestKnownValue.MeasuredEnergyWs.HasValue)
            {
                meterValue.EstimatedEnergyWs = latestKnownValue.MeasuredEnergyWs + energySinceLastValue;
            }
            else
            {
                meterValue.EstimatedEnergyWs = latestKnownValue.EstimatedEnergyWs + energySinceLastValue;
            }

        }

        // Fill in EstimatedPower if MeasuredPower is missing.
        if (!meterValue.MeasuredPower.HasValue)
        {
            // If both current and previous energy readings are available, compute the power.
            if (meterValue.MeasuredEnergyWs.HasValue && latestKnownValue.MeasuredEnergyWs.HasValue)
            {
                var energyDifference = meterValue.MeasuredEnergyWs.Value - latestKnownValue.MeasuredEnergyWs.Value;
                if (elapsedSeconds == 0)
                {
                    meterValue.EstimatedPower = 0;
                }
                else
                {
                    meterValue.EstimatedPower = (int)(energyDifference / elapsedSeconds);
                }

            }
            else
            {
                // Otherwise, fallback to the previous known power value.
                meterValue.EstimatedPower = latestKnownValue.MeasuredPower ?? 0;
            }
        }

        // If the current record has any measured (real) values, update the latest known value
        // so subsequent records use this as the baseline.
        if (meterValue.MeasuredEnergyWs.HasValue || meterValue.MeasuredPower.HasValue)
        {
            latestKnownValue = meterValue;
        }

        return latestKnownValue;
    }
}
