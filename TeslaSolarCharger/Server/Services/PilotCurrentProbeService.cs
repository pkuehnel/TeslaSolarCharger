using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

/// <summary>
/// Plug in times can not tell which car is plugged into an OCPP connector: a charge point reported its plug in two
/// minutes after the car did, and a car plugged into another wallbox at the same time as a guest car into the OCPP
/// connector matches just as well. The pilot current can: it is the current the connector offers, and the car
/// reports it within a few seconds of the connector changing it.
/// </summary>
public class PilotCurrentProbeService(
    ILogger<PilotCurrentProbeService> logger,
    ITeslaSolarChargerContext context,
    ISettings settings,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    ILoadPointManagementService loadPointManagementService,
    IOcppChargePointActionService ocppChargePointActionService,
    IBleVehicleDataService bleVehicleDataService) : IPilotCurrentProbeService
{
    /// <summary>
    /// The lowest current IEC 61851 allows a charge point to offer. Below it the connector offers nothing, which a car
    /// reports the same as no transaction at all (measured: a 0 A limit reads as the idle value of 5 A).
    /// </summary>
    private const int MinimumProbeCurrent = 6;
    private const int MaxPolls = 30;
    private const int PollsBetweenBleRefreshes = 5;

    internal TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether the pilot current of a car arrives fast enough to see the reaction to a probe: Fleet Telemetry pushes
    /// it, BLE can be read on demand. Polled Fleet API or TeslaMate data are too slow, so those cars keep being matched
    /// by their plug in time.
    /// </summary>
    public static bool CanObservePilotCurrent(CarType carType, bool useFleetTelemetry, bool useBle,
        IConfigurationWrapper configurationWrapper)
    {
        if (carType != CarType.Tesla || !configurationWrapper.GetVehicleDataFromTesla())
        {
            return false;
        }
        return useFleetTelemetry || (useBle && configurationWrapper.GetVehicleDataViaBle());
    }

    public async Task ProbeUnassignedConnectors(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(ProbeUnassignedConnectors));
        var combinations = await loadPointManagementService.GetCombinationsToManage().ConfigureAwait(false);
        var unassignedConnectorIds = combinations
            .Where(c => c.ChargingConnectorId != default && c.CarId == default)
            .Select(c => c.ChargingConnectorId!.Value)
            .ToHashSet();
        if (unassignedConnectorIds.Count < 1)
        {
            return;
        }
        var assignedCarIds = combinations
            .Where(c => c.ChargingConnectorId != default && c.CarId != default)
            .Select(c => c.CarId!.Value)
            .ToHashSet();
        var observableCarIds = (await context.Cars
                .Where(c => c.ShouldBeManaged == true)
                .Select(c => new { c.Id, c.CarType, c.UseFleetTelemetry, c.UseBle, })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Where(c => CanObservePilotCurrent(c.CarType, c.UseFleetTelemetry, c.UseBle, configurationWrapper))
            .Select(c => c.Id)
            .ToHashSet();
        var connectors = await context.OcppChargingStationConnectors
            .Where(c => unassignedConnectorIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.MinCurrent,
                c.MaxCurrent,
                AllowedCarIds = c.AllowedCars.Select(ac => ac.CarId).ToList(),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var connector in connectors)
        {
            if (!settings.OcppConnectorStates.TryGetValue(connector.Id, out var connectorState)
                || !connectorState.IsPluggedIn.Value)
            {
                continue;
            }
            if (IsSetToGuestCarByUser(connector.Id, connectorState))
            {
                logger.LogTrace("Connector {connectorId} was set to a guest car by the user, do not probe it", connector.Id);
                continue;
            }
            var candidates = settings.Cars
                .Where(c => observableCarIds.Contains(c.Id)
                            && connector.AllowedCarIds.Contains(c.Id)
                            && !assignedCarIds.Contains(c.Id)
                            && c.IsHomeGeofence.Value == true
                            && c.PluggedIn.Value == true)
                .ToList();
            if (candidates.Count < 1 || WasProbedSincePlugIn(connector.Id, connectorState, candidates))
            {
                continue;
            }
            var matchedCarId = await ProbeConnector(connector.Id, connector.MinCurrent, connector.MaxCurrent,
                connectorState, candidates, cancellationToken).ConfigureAwait(false);
            if (matchedCarId != default)
            {
                assignedCarIds.Add(matchedCarId.Value);
            }
        }
    }

    private bool IsSetToGuestCarByUser(int connectorId, DtoOcppConnectorState connectorState)
    {
        return settings.ConfirmedLoadPointCarCombinations.TryGetValue(connectorId, out var combination)
               && combination.carId == default
               && combination.combinationTimeStamp >= connectorState.IsPluggedIn.LastChanged;
    }

    private bool WasProbedSincePlugIn(int connectorId, DtoOcppConnectorState connectorState, List<DtoCar> candidates)
    {
        if (!settings.LastPilotCurrentProbes.TryGetValue(connectorId, out var lastProbe))
        {
            return false;
        }
        return lastProbe >= connectorState.IsPluggedIn.LastChanged
               && candidates.All(c => lastProbe >= c.PluggedIn.LastChanged);
    }

    /// <returns>The id of the car plugged into the connector, or null if no single candidate reacted.</returns>
    private async Task<int?> ProbeConnector(int connectorId, int? minCurrent, int? maxCurrent,
        DtoOcppConnectorState connectorState, List<DtoCar> candidates, CancellationToken cancellationToken)
    {
        settings.LastPilotCurrentProbes[connectorId] = dateTimeProvider.DateTimeOffSetUtcNow();
        var probeCurrent = SelectProbeCurrent(minCurrent, maxCurrent, candidates.Select(c => c.ChargerPilotCurrent.Value));
        if (probeCurrent == default)
        {
            logger.LogWarning("Can not probe connector {connectorId}: every current it can offer is already reported as pilot current by a candidate car", connectorId);
            return null;
        }
        logger.LogTrace("Probing connector {connectorId} with {probeCurrent} A to find out which of the cars {@carIds} is plugged into it",
            connectorId, probeCurrent, candidates.Select(c => c.Id));
        var wasCharging = connectorState.IsCharging.Value;
        var lastSetCurrent = connectorState.LastSetCurrent.Value;
        var startResult = await ocppChargePointActionService
            .StartCharging(connectorId, probeCurrent.Value, null, cancellationToken).ConfigureAwait(false);
        if (startResult.HasError)
        {
            logger.LogWarning("Could not offer probe current on connector {connectorId}: {error}", connectorId, startResult.ErrorMessage);
            return null;
        }
        var matchedCarId = await WaitForReaction(candidates, probeCurrent.Value, cancellationToken).ConfigureAwait(false);
        await RestoreConnector(connectorId, wasCharging, lastSetCurrent, cancellationToken).ConfigureAwait(false);
        if (matchedCarId == default)
        {
            logger.LogTrace("No single car reacted to the probe of connector {connectorId}", connectorId);
            return null;
        }
        logger.LogTrace("Car {carId} is plugged into connector {connectorId}", matchedCarId, connectorId);
        settings.ConfirmedLoadPointCarCombinations[connectorId] = (matchedCarId, dateTimeProvider.DateTimeOffSetUtcNow());
        return matchedCarId;
    }

    /// <summary>
    /// The lowest current the connector can offer that no candidate currently reports, so a candidate showing it can
    /// only be reacting to the probe.
    /// </summary>
    internal static int? SelectProbeCurrent(int? minCurrent, int? maxCurrent, IEnumerable<int?> pilotCurrents)
    {
        if (maxCurrent == default)
        {
            return null;
        }
        var reportedCurrents = pilotCurrents.ToHashSet();
        for (var current = Math.Max(MinimumProbeCurrent, minCurrent ?? MinimumProbeCurrent); current <= maxCurrent; current++)
        {
            if (!reportedCurrents.Contains(current))
            {
                return current;
            }
        }
        return null;
    }

    private async Task<int?> WaitForReaction(List<DtoCar> candidates, int probeCurrent, CancellationToken cancellationToken)
    {
        for (var poll = 1; poll <= MaxPolls; poll++)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            if (poll % PollsBetweenBleRefreshes == 0)
            {
                foreach (var candidate in candidates)
                {
                    //Does nothing for cars that do not collect their data via BLE.
                    await bleVehicleDataService.RefreshSingleCarData(candidate.Id).ConfigureAwait(false);
                }
            }
            var reactedCars = candidates.Where(c => c.ChargerPilotCurrent.Value == probeCurrent).ToList();
            if (reactedCars.Count == 1)
            {
                return reactedCars[0].Id;
            }
            if (reactedCars.Count > 1)
            {
                logger.LogWarning("Cars {@carIds} all report the probe current of {probeCurrent} A, can not decide which one is plugged in",
                    reactedCars.Select(c => c.Id), probeCurrent);
                return null;
            }
        }
        return null;
    }

    private async Task RestoreConnector(int connectorId, bool wasCharging, decimal? lastSetCurrent, CancellationToken cancellationToken)
    {
        if (!wasCharging)
        {
            var stopResult = await ocppChargePointActionService.StopCharging(connectorId, cancellationToken).ConfigureAwait(false);
            if (stopResult.HasError)
            {
                logger.LogWarning("Could not stop probe charging on connector {connectorId}: {error}", connectorId, stopResult.ErrorMessage);
            }
            return;
        }
        if (lastSetCurrent == default)
        {
            return;
        }
        var setResult = await ocppChargePointActionService
            .SetChargingCurrent(connectorId, lastSetCurrent.Value, null, cancellationToken).ConfigureAwait(false);
        if (setResult.HasError)
        {
            logger.LogWarning("Could not restore current {current} A on connector {connectorId}: {error}", lastSetCurrent, connectorId, setResult.ErrorMessage);
        }
    }
}
