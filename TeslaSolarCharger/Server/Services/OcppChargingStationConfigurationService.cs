using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class OcppChargingStationConfigurationService(ILogger<OcppChargingStationConfigurationService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IOcppChargePointConfigurationService ocppChargePointConfigurationService,
    ISettings settings) : IOcppChargingStationConfigurationService
{
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
        var connectedChargingConnectorIds = settings.OcppConnectorStates.Keys.ToList();
        var connectedChargePointIds = await teslaSolarChargerContext.OcppChargingStationConnectors
            .Where(cc => connectedChargingConnectorIds.Contains(cc.Id))
            .Select(cc => cc.OcppChargingStation.ChargepointId)
            .Distinct()
            .ToHashSetAsync().ConfigureAwait(false);
        foreach (var chargingStation in chargingStations)
        {
            chargingStation.IsConnected = connectedChargePointIds.Contains(chargingStation.ChargepointId);
        }
        return chargingStations;
    }

    public async Task<List<DtoChargingStationConnector>> GetChargingStationConnectors(int chargingStationId)
    {
        logger.LogTrace("{method}({chargingStationId})", nameof(GetChargingStationConnectors), chargingStationId);
        var chargingConnectors = await teslaSolarChargerContext.OcppChargingStationConnectors
            .Where(cc => cc.OcppChargingStationId == chargingStationId)
            .OrderBy(c => c.ConnectorId)
            .Select(cc => new DtoChargingStationConnector(cc.Name)
            {
                Id = cc.Id,
                ChargingStationId = cc.OcppChargingStationId,
                ShouldBeManaged = cc.ShouldBeManaged,
                ConnectorId = cc.ConnectorId,
                AutoSwitchBetween1And3PhasesEnabled = cc.AutoSwitchBetween1And3PhasesEnabled,
                PhaseSwitchCoolDownTimeSeconds = cc.PhaseSwitchCoolDownTimeSeconds,
                MinCurrent = cc.MinCurrent,
                SwitchOffAtCurrent = cc.SwitchOffAtCurrent,
                SwitchOnAtCurrent = cc.SwitchOnAtCurrent,
                MaxCurrent = cc.MaxCurrent,
                ConnectedPhasesCount = cc.ConnectedPhasesCount ?? 3,
                ChargingPriority = cc.ChargingPriority,
            })
            .ToListAsync().ConfigureAwait(false);
        return chargingConnectors;
    }

    public async Task UpdateChargingStationConnector(DtoChargingStationConnector dtoChargingStation)
    {
        logger.LogTrace("{method}({@dto})", nameof(UpdateChargingStationConnector), dtoChargingStation);
        var existingChargingStation = await teslaSolarChargerContext.OcppChargingStationConnectors.FirstAsync(c => c.Id == dtoChargingStation.Id);
        existingChargingStation.Name = dtoChargingStation.Name;
        existingChargingStation.ShouldBeManaged = dtoChargingStation.ShouldBeManaged;
        existingChargingStation.MinCurrent = dtoChargingStation.MinCurrent;
        existingChargingStation.SwitchOffAtCurrent = dtoChargingStation.SwitchOffAtCurrent;
        existingChargingStation.SwitchOnAtCurrent = dtoChargingStation.SwitchOnAtCurrent;
        existingChargingStation.MaxCurrent = dtoChargingStation.MaxCurrent;
        existingChargingStation.ConnectedPhasesCount = dtoChargingStation.ConnectedPhasesCount;
        existingChargingStation.AutoSwitchBetween1And3PhasesEnabled = dtoChargingStation.AutoSwitchBetween1And3PhasesEnabled;
        existingChargingStation.PhaseSwitchCoolDownTimeSeconds = dtoChargingStation.PhaseSwitchCoolDownTimeSeconds;
        existingChargingStation.ChargingPriority = dtoChargingStation.ChargingPriority;
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
        var reconfigurationRequiredResult = await ocppChargePointConfigurationService.IsReconfigurationRequired(chargepointId, httpContextRequestAborted);
        if (reconfigurationRequiredResult.HasError)
        {
            logger.LogError("Could not check if reconfiguration is required for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, reconfigurationRequiredResult.ErrorMessage);
            return;
        }
        if (reconfigurationRequiredResult.Data == true)
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
            var clockAlignedDataResult = await ocppChargePointConfigurationService.SetMeterValuesClockAligedDataConfiguration(chargepointId, httpContextRequestAborted);
            if (clockAlignedDataResult.HasError)
            {
                logger.LogError("Could not set MeterValuesClockAligedDataConfiguration for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, meterValueSampledDataResult.ErrorMessage);
                return;
            }
            //Can not be null if HasError is false
            if (clockAlignedDataResult.Data!.Status == ConfigurationStatus.RebootRequired)
            {
                rebootIsRequired = true;
            }
            var clockAlignedDataIntervalResult = await ocppChargePointConfigurationService.SetMeterValuesClockAlignedIntervalConfiguration(chargepointId, httpContextRequestAborted);
            if (clockAlignedDataIntervalResult.HasError)
            {
                logger.LogError("Could not set MeterValuesClockAlignedIntervalConfiguration for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, meterValueSampleIntervallResult.ErrorMessage);
                return;
            }
            //Can not be null if HasError is false
            if (clockAlignedDataIntervalResult.Data!.Status == ConfigurationStatus.RebootRequired)
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
                existingChargingStation.Connectors.Add(new(existingChargingStation.ChargepointId + "; Connector: " + (i + 1))
                {
                    ConnectorId = i + 1,
                });
            }
        }
        foreach (var ocppChargingStationConnector in existingChargingStation.Connectors)
        {
            ocppChargingStationConnector.ShouldBeManaged = true;
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
