using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;

namespace TeslaSolarCharger.Server.Services;

public class OcppChargingStationConfigurationService(ILogger<OcppChargingStationConfigurationService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IOcppChargePointConfigurationService ocppChargePointConfigurationService) : IOcppChargingStationConfigurationService
{
    private const int CurrentConfigurationVersion = 1;

    public async Task<List<DtoChargingStation>> GetChargingStations()
    {
        logger.LogTrace("{method}()", nameof(GetChargingStations));
        var chargingStations = await teslaSolarChargerContext.OcppChargingStations
            .Select(c => new DtoChargingStation(c.ChargepointId)
            {
                Id = c.Id,
                CanSwitchBetween1And3Phases = c.CanSwitchBetween1And3Phases,
            })
            .ToListAsync().ConfigureAwait(false);
        return chargingStations;
    }

    public async Task UpdateChargingStationConnector(DtoChargingStationConnector dtoChargingStation)
    {
        logger.LogTrace("{method}({@dto})", nameof(UpdateChargingStationConnector), dtoChargingStation);
        var existingChargingStation = await teslaSolarChargerContext.OcppChargingStationConnectors.FirstAsync(c => c.Id == dtoChargingStation.Id);
        existingChargingStation.MaxCurrent = dtoChargingStation.MaxCurrent;
        existingChargingStation.AutoSwitchBetween1And3PhasesEnabled = dtoChargingStation.AutoSwitchBetween1And3PhasesEnabled;
        await teslaSolarChargerContext.SaveChangesAsync();
    }

    public async Task AddChargingStationIfNotExisting(string chargepointId, CancellationToken httpContextRequestAborted)
    {
        logger.LogTrace("{method}({chargepointId})", nameof(AddChargingStationIfNotExisting), chargepointId);
        var existingChargingStation = await teslaSolarChargerContext.OcppChargingStations
            .Include(c => c.Connectors)
            .FirstOrDefaultAsync(x => x.ChargepointId == chargepointId, cancellationToken: httpContextRequestAborted);

        if (existingChargingStation == default)
        {
            existingChargingStation = new(chargepointId);
            teslaSolarChargerContext.OcppChargingStations.Add(existingChargingStation);
        }

        if (existingChargingStation.ConfigurationVersion != CurrentConfigurationVersion)
        {
            var reconfigurationRequiredResult = await ocppChargePointConfigurationService.IsReconfigurationRequired(chargepointId, httpContextRequestAborted);
            if (reconfigurationRequiredResult.HasError)
            {
                logger.LogError("Could not check if reconfiguration is required for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, reconfigurationRequiredResult.ErrorMessage);
                return;
            }
            if(reconfigurationRequiredResult.Data == true)
            {
                var rebootIsRequired = false;
                logger.LogInformation("Reconfiguration is required for charge point {chargePointId}.", chargepointId);
                var meterValueSampledDataResult = await ocppChargePointConfigurationService.SetMeterValuesSampledDataConfiguration(chargepointId, httpContextRequestAborted);
                if (meterValueSampledDataResult.HasError)
                {
                    logger.LogError("Could not set MeterValuesSampledDataConfiguration for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, meterValueSampledDataResult.ErrorMessage);
                    return;
                }
                //Can not be null if HasError is false
                if (meterValueSampledDataResult.Data!.Status == ConfigurationStatus.RebootRequired)
                {
                    rebootIsRequired = true;
                }
                var meterValueSampleIntervallResult = await ocppChargePointConfigurationService.SetMeterValuesSampleIntervalConfiguration(chargepointId, httpContextRequestAborted);
                if (meterValueSampleIntervallResult.HasError)
                {
                    logger.LogError("Could not set MeterValuesSampleIntervalConfiguration for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, meterValueSampleIntervallResult.ErrorMessage);
                    return;
                }
                //Can not be null if HasError is false
                if (meterValueSampleIntervallResult.Data!.Status == ConfigurationStatus.RebootRequired)
                {
                    rebootIsRequired = true;
                }
                if (rebootIsRequired)
                {
                    var rebootResult = await ocppChargePointConfigurationService.RebootCharger(chargepointId, httpContextRequestAborted);
                    if (rebootResult.HasError)
                    {
                        logger.LogError("Could not reboot charge point {chargePointId}. Error message: {errorMessage}", chargepointId, rebootResult.ErrorMessage);
                        return;
                    }
                }
            }
            else
            {
                logger.LogInformation("Reconfiguration is not required for charge point {chargePointId}.", chargepointId);
            }
            existingChargingStation.ConfigurationVersion = CurrentConfigurationVersion;
        }
        var numberOfConnectors = await ocppChargePointConfigurationService.NumberOfConnectors(chargepointId, httpContextRequestAborted);
        if (numberOfConnectors.HasError)
        {
            logger.LogError("Could not get number of connectors for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, numberOfConnectors.ErrorMessage);
            return;
        }
        if (numberOfConnectors.Data > existingChargingStation.Connectors.Count)
        {
            logger.LogInformation("Adding {numberOfConnectors} connectors to charge point {chargePointId}.", numberOfConnectors.Data, chargepointId);
            for (var i = existingChargingStation.Connectors.Count; i < numberOfConnectors.Data; i++)
            {
                existingChargingStation.Connectors.Add(new OcppChargingStationConnector
                {
                    ConnectorId = i + 1,
                });
            }
        }
        var canSwitchPhases = await ocppChargePointConfigurationService.CanSwitchBetween1And3Phases(chargepointId, httpContextRequestAborted);
        if (canSwitchPhases.HasError)
        {
            logger.LogError("Could not get can switch phases for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, numberOfConnectors.ErrorMessage);
            return;
        }
        existingChargingStation.CanSwitchBetween1And3Phases = canSwitchPhases.Data;
        await teslaSolarChargerContext.SaveChangesAsync(httpContextRequestAborted);
    }
}
