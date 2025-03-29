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
    public async Task UpdateEstimatedMeterValues()
    {
        logger.LogTrace("{method}()", nameof(UpdateEstimatedMeterValues));
        var latestUpdatedId = await GetLatestEstimatedMeterValueId();
        var newLatestUpdatedId = latestUpdatedId ?? 0;
        var meterValuesQuery = context.MeterValues.AsQueryable();
        if (latestUpdatedId != default)
        {
            meterValuesQuery = meterValuesQuery.Where(v => v.Id > latestUpdatedId.Value);
        }
        var meterValuesToUpdateGroups = await meterValuesQuery
            .OrderBy(v => v.Id)
            .GroupBy(m => m.MeterValueKind)
            .ToListAsync();
        foreach (var group in meterValuesToUpdateGroups)
        {
            if (!group.Any())
            {
                continue;
            }
            MeterValue latestKnownValue;
            if (latestUpdatedId == default)
            {
                latestKnownValue = new MeterValue()
                {
                    Id = 0,
                    Timestamp = group.First().Timestamp,
                    MeterValueKind = group.Key,
                    EstimatedEnergyWs = 0,
                    EstimatedPower = 0,
                    MeasuredEnergyWs = 0,
                    MeasuredPower = 0,
                };
            }
            else
            {
                var latestKnownDatabaseValue = await context.MeterValues
                    .Where(v => v.Id <= latestUpdatedId.Value && v.MeterValueKind == group.Key)
                    .OrderByDescending(v => v.Id)
                    .FirstOrDefaultAsync();
                if (latestKnownDatabaseValue == default)
                {
                    latestKnownValue = new MeterValue()
                    {
                        Id = 0,
                        Timestamp = group.First().Timestamp,
                        MeterValueKind = group.Key,
                        EstimatedEnergyWs = 0,
                        EstimatedPower = 0,
                        MeasuredEnergyWs = 0,
                        MeasuredPower = 0,
                    };
                }
                else
                {
                    latestKnownValue = latestKnownDatabaseValue;
                }
            }
            foreach (var meterValue in group)
            {
                // Calculate the time difference (in hours) between the current record and the previous known value.
                // Note: Ensure that Timestamp is set properly for both values.
                var elapsedSeconds = (meterValue.Timestamp - latestKnownValue.Timestamp).TotalSeconds;
                if (elapsedSeconds < 0)
                {
                    // Handle edge cases like identical timestamps.
                    logger.LogWarning("The timestamp of the meter value {@meterValue} is not not newer than the last meterValue {@lastMeterValueId}", meterValue, latestKnownValue);
                    throw new InvalidDataException(
                        $"The timestamp of the meter value {meterValue.Id} is not not newer than the last meterValue {latestKnownValue.Id}");
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
                    if (newLatestUpdatedId < meterValue.Id)
                    {
                        newLatestUpdatedId = meterValue.Id;
                    }
                }
            }

            await context.SaveChangesAsync();
            await tscConfigurationService.SetConfigurationValueByKey(constants.LatestEstimatedMeterValueIdKey,
                newLatestUpdatedId.ToString());
        }
    }


    private async Task<int?> GetLatestEstimatedMeterValueId()
    {
        logger.LogTrace("{method}()", nameof(GetLatestEstimatedMeterValueId));
        var idString = await tscConfigurationService.GetConfigurationValueByKey(constants.LatestEstimatedMeterValueIdKey);
        if (string.IsNullOrEmpty(idString))
        {
            return null;
        }
        if (int.TryParse(idString, out var id))
        {
            return id;
        }
        return null;
       
    }
}
