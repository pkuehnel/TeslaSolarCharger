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
                AllowedCars = cc.AllowedCars.Select(ac => ac.CarId).ToHashSet(),
                AllowGuestCars = cc.AllowGuestCars,
            })
            .ToListAsync().ConfigureAwait(false);
        return chargingConnectors;
    }

    public async Task<Dictionary<int, string>> GetCarOptions()
    {
        logger.LogTrace("{method}()", nameof(GetCarOptions));
        var result = await teslaSolarChargerContext.Cars
            .Where(c => c.ShouldBeManaged == true)
            .ToDictionaryAsync(c => c.Id, c => c.Name ?? c.Vin ?? "NoName");
        return result;
    }

    public async Task UpdateChargingStationConnector(DtoChargingStationConnector dtoChargingStation)
    {
        logger.LogTrace("{method}({@dto})", nameof(UpdateChargingStationConnector), dtoChargingStation);
        var existingChargingStation = await teslaSolarChargerContext.OcppChargingStationConnectors
            .Include(c => c.AllowedCars)
            .FirstAsync(c => c.Id == dtoChargingStation.Id);
        existingChargingStation.Name = dtoChargingStation.Name;
        existingChargingStation.ShouldBeManaged = dtoChargingStation.ShouldBeManaged || settings.OcppConnectorStates.ContainsKey(dtoChargingStation.Id);
        existingChargingStation.MinCurrent = dtoChargingStation.MinCurrent;
        existingChargingStation.SwitchOffAtCurrent = dtoChargingStation.SwitchOffAtCurrent;
        existingChargingStation.SwitchOnAtCurrent = dtoChargingStation.SwitchOnAtCurrent;
        existingChargingStation.MaxCurrent = dtoChargingStation.MaxCurrent;
        existingChargingStation.ConnectedPhasesCount = dtoChargingStation.ConnectedPhasesCount;
        existingChargingStation.AutoSwitchBetween1And3PhasesEnabled = dtoChargingStation.AutoSwitchBetween1And3PhasesEnabled;
        existingChargingStation.PhaseSwitchCoolDownTimeSeconds = dtoChargingStation.PhaseSwitchCoolDownTimeSeconds;
        existingChargingStation.ChargingPriority = dtoChargingStation.ChargingPriority;
        existingChargingStation.AllowGuestCars = dtoChargingStation.AllowGuestCars;

        var existingCarIds = existingChargingStation.AllowedCars
            .Select(ac => ac.CarId)
            .ToHashSet();

        var dtoCarIds = dtoChargingStation.AllowedCars;

        existingChargingStation.AllowedCars
            .RemoveAll(ac => !dtoCarIds.Contains(ac.CarId));

        foreach (var carId in dtoCarIds.Except(existingCarIds))
        {
            existingChargingStation.AllowedCars.Add(new()
            {
                CarId = carId,
                OcppChargingStationConnectorId = existingChargingStation.Id,
            });
        }
        await teslaSolarChargerContext.SaveChangesAsync();
    }

    public async Task DeleteChargingStation(int chargingStationId)
    {
        logger.LogTrace("{method}({chargingStationId})", nameof(DeleteChargingStation), chargingStationId);
        var stationExists = await teslaSolarChargerContext.OcppChargingStations
            .AnyAsync(s => s.Id == chargingStationId).ConfigureAwait(false);
        if (!stationExists)
        {
            logger.LogWarning("Charging station with id {chargingStationId} does not exist, nothing to delete.", chargingStationId);
            return;
        }

        var connectorIds = await teslaSolarChargerContext.OcppChargingStationConnectors
            .Where(c => c.OcppChargingStationId == chargingStationId)
            .Select(c => c.Id)
            .ToListAsync().ConfigureAwait(false);
        // Nullable copy for the IN clauses on the (nullable) FK columns of ChargingProcess and MeterValue.
        var nullableConnectorIds = connectorIds.Select(id => (int?)id).ToList();

        // Remove every row that references the station's connectors before deleting the connectors and the
        // station itself, otherwise the foreign key constraints would block the delete. Children are removed
        // before their parents so no constraint is violated at any committed step. This mirrors the car deletion
        // (see ConfigJsonService.DeleteCar): it is intentionally NOT wrapped in a single transaction, and the
        // potentially large tables (connector value logs, meter values) are deleted in batches so SQLite's
        // database-wide write lock is released between batches instead of being held for the whole delete. A
        // failure midway leaves a partially deleted station, but the delete is idempotent (re-running removes
        // whatever is left) and the child-before-parent order keeps the database consistent at every step.
        const int batchSize = 10_000;

        if (connectorIds.Count > 0)
        {
            // Charging processes and their details.
            // A charging process can belong to both a car and a connector (a car charging at this station).
            // Those linked to a car are car history and must be kept - only the connector reference is removed.
            // Charging processes without a car relation are connector-only and are deleted together with their
            // details. Details of kept (car) processes must not be deleted.
            await teslaSolarChargerContext.ChargingDetails
                .Where(cd => cd.ChargingProcess.CarId == null
                             && nullableConnectorIds.Contains(cd.ChargingProcess.OcppChargingStationConnectorId))
                .ExecuteDeleteAsync().ConfigureAwait(false);
            await teslaSolarChargerContext.ChargingProcesses
                .Where(cp => cp.CarId == null && nullableConnectorIds.Contains(cp.OcppChargingStationConnectorId))
                .ExecuteDeleteAsync().ConfigureAwait(false);
            await teslaSolarChargerContext.ChargingProcesses
                .Where(cp => cp.CarId != null && nullableConnectorIds.Contains(cp.OcppChargingStationConnectorId))
                .ExecuteUpdateAsync(s => s.SetProperty(cp => cp.OcppChargingStationConnectorId, (int?)null))
                .ConfigureAwait(false);

            // OCPP transactions of the connectors.
            await teslaSolarChargerContext.OcppTransactions
                .Where(t => connectorIds.Contains(t.ChargingStationConnectorId))
                .ExecuteDeleteAsync().ConfigureAwait(false);

            // Logged connector values (potentially a very large table). Deleted in batches so SQLite's write
            // lock is released between batches instead of being held for one huge delete statement.
            while (true)
            {
                var batchIds = await teslaSolarChargerContext.OcppChargingStationConnectorValueLogs
                    .Where(vl => connectorIds.Contains(vl.OcppChargingStationConnectorId))
                    .OrderBy(vl => vl.Id)
                    .Select(vl => vl.Id)
                    .Take(batchSize)
                    .ToListAsync().ConfigureAwait(false);
                if (batchIds.Count == 0)
                {
                    break;
                }
                await teslaSolarChargerContext.OcppChargingStationConnectorValueLogs
                    .Where(vl => batchIds.Contains(vl.Id))
                    .ExecuteDeleteAsync().ConfigureAwait(false);
                if (batchIds.Count < batchSize)
                {
                    break;
                }
            }

            // Meter values (potentially a very large table).
            // A meter value belongs to either a car or a connector, never both (see the CK_MeterValue_CarId
            // check constraint), so every meter value with one of these ChargingConnectorIds is connector-only
            // and safe to delete (car meter values are not affected). Deleted in batches for the same reason as
            // the connector value logs above.
            while (true)
            {
                var batchIds = await teslaSolarChargerContext.MeterValues
                    .Where(mv => nullableConnectorIds.Contains(mv.ChargingConnectorId))
                    .OrderBy(mv => mv.Id)
                    .Select(mv => mv.Id)
                    .Take(batchSize)
                    .ToListAsync().ConfigureAwait(false);
                if (batchIds.Count == 0)
                {
                    break;
                }
                await teslaSolarChargerContext.MeterValues
                    .Where(mv => batchIds.Contains(mv.Id))
                    .ExecuteDeleteAsync().ConfigureAwait(false);
                if (batchIds.Count < batchSize)
                {
                    break;
                }
            }

            // Allowed car assignments of the connectors.
            await teslaSolarChargerContext.ChargingStationConnectorAllowedCars
                .Where(ac => connectorIds.Contains(ac.OcppChargingStationConnectorId))
                .ExecuteDeleteAsync().ConfigureAwait(false);

            // The connectors themselves.
            await teslaSolarChargerContext.OcppChargingStationConnectors
                .Where(c => connectorIds.Contains(c.Id))
                .ExecuteDeleteAsync().ConfigureAwait(false);
        }

        // Finally the charging station itself.
        await teslaSolarChargerContext.OcppChargingStations
            .Where(s => s.Id == chargingStationId)
            .ExecuteDeleteAsync().ConfigureAwait(false);

        // Drop the connectors from the in-memory state so the charging loop and load point handling stop
        // referencing them. LatestLoadPointCombinations is replaced (not mutated in place) so a reader iterating
        // it on the charging loop thread does not risk a "Collection was modified" exception.
        foreach (var connectorId in connectorIds)
        {
            settings.OcppConnectorStates.TryRemove(connectorId, out _);
            settings.ChargingConnectorsWithNonZeroMeterValueAddedLastCycle.TryRemove(connectorId, out _);
        }
        settings.LatestLoadPointCombinations = settings.LatestLoadPointCombinations
            .Where(lp => lp.ChargingConnectorId == null || !connectorIds.Contains(lp.ChargingConnectorId.Value))
            .ToHashSet();
    }

    public async Task AddChargingStationIfNotExisting(string chargepointId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargepointId})", nameof(AddChargingStationIfNotExisting), chargepointId);
        var existingChargingStation = await teslaSolarChargerContext.OcppChargingStations
            .Include(c => c.Connectors)
            .FirstOrDefaultAsync(x => x.ChargepointId == chargepointId, cancellationToken: cancellationToken);

        if (existingChargingStation == default)
        {
            existingChargingStation = new(chargepointId);
            teslaSolarChargerContext.OcppChargingStations.Add(existingChargingStation);
        }
        var reconfigurationRequiredResult = await ocppChargePointConfigurationService.IsReconfigurationRequired(chargepointId, cancellationToken);
        if (reconfigurationRequiredResult.HasError)
        {
            logger.LogError("Could not check if reconfiguration is required for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, reconfigurationRequiredResult.ErrorMessage);
            return;
        }
        if (reconfigurationRequiredResult.Data == true)
        {
            var rebootIsRequired = false;
            logger.LogInformation("Reconfiguration is required for charge point {chargePointId}.", chargepointId);
            var meterValueSampledDataResult = await ocppChargePointConfigurationService.SetMeterValuesSampledDataConfiguration(chargepointId, cancellationToken);
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
            var meterValueSampleIntervallResult = await ocppChargePointConfigurationService.SetMeterValuesSampleIntervalConfiguration(chargepointId, cancellationToken);
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
            var clockAlignedDataResult = await ocppChargePointConfigurationService.SetMeterValuesClockAligedDataConfiguration(chargepointId, cancellationToken);
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
            var clockAlignedDataIntervalResult = await ocppChargePointConfigurationService.SetMeterValuesClockAlignedIntervalConfiguration(chargepointId, cancellationToken);
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
                var rebootResult = await ocppChargePointConfigurationService.RebootCharger(chargepointId, cancellationToken);
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
        var numberOfConnectors = await ocppChargePointConfigurationService.NumberOfConnectors(chargepointId, cancellationToken);
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
        var canSwitchPhases = await ocppChargePointConfigurationService.CanSwitchBetween1And3Phases(chargepointId, cancellationToken);
        if (canSwitchPhases.HasError)
        {
            logger.LogError("Could not get can switch phases for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, numberOfConnectors.ErrorMessage);
            return;
        }
        existingChargingStation.CanSwitchBetween1And3Phases = canSwitchPhases.Data;
        await teslaSolarChargerContext.SaveChangesAsync(cancellationToken);
    }
}
