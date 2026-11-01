using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
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
    IBleVehicleDataService bleVehicleDataService,
    ITeslaService teslaService) : IPilotCurrentProbeService
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

    public async Task<int> ProbeUnassignedConnectors(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(ProbeUnassignedConnectors));
        var combinations = await loadPointManagementService.GetCombinationsToManage().ConfigureAwait(false);
        var unassignedConnectorIds = combinations
            .Where(c => c.ChargingConnectorId != default && c.CarId == default)
            .Select(c => c.ChargingConnectorId!.Value)
            .ToHashSet();
        if (unassignedConnectorIds.Count < 1)
        {
            return 0;
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
        var maxCombinedCurrent = configurationWrapper.MaxCombinedCurrent();
        var chargingLoadPoints = await loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var currentToReserve = 0;
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
            var probeCurrent = SelectProbeCurrent(connector.MinCurrent, connector.MaxCurrent, candidates.Select(c => c.ChargerPilotCurrent.Value));
            if (probeCurrent == default)
            {
                settings.LastPilotCurrentProbes[connector.Id] = dateTimeProvider.DateTimeOffSetUtcNow();
                logger.LogWarning("Can not probe connector {connectorId}: every current it can offer is already reported as pilot current by a candidate car", connector.Id);
                continue;
            }
            //The charging cycle waits for the probe, so no other load point draws more than it does now until the probe ends.
            var currentUsedByOtherLoadPoints = chargingLoadPoints
                .Where(l => l.ChargingConnectorId != connector.Id)
                .Sum(l => l.ChargingCurrent);
            if (currentUsedByOtherLoadPoints + probeCurrent.Value > maxCombinedCurrent)
            {
                logger.LogTrace("Probe current of {probeCurrent} A on connector {connectorId} would exceed the max combined current of {maxCombinedCurrent} A as other load points use {usedCurrent} A, reserve it",
                    probeCurrent, connector.Id, maxCombinedCurrent, currentUsedByOtherLoadPoints);
                currentToReserve = Math.Max(currentToReserve, probeCurrent.Value);
                continue;
            }
            var matchedCarId = await ProbeConnector(connector.Id, probeCurrent.Value, connector.MaxCurrent!.Value, connectorState, candidates, cancellationToken)
                .ConfigureAwait(false);
            if (matchedCarId != default)
            {
                assignedCarIds.Add(matchedCarId.Value);
            }
        }
        return currentToReserve;
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
    private async Task<int?> ProbeConnector(int connectorId, int probeCurrent, int maxCurrent,
        DtoOcppConnectorState connectorState, List<DtoCar> candidates, CancellationToken cancellationToken)
    {
        settings.LastPilotCurrentProbes[connectorId] = dateTimeProvider.DateTimeOffSetUtcNow();
        logger.LogTrace("Probing connector {connectorId} with {probeCurrent} A to find out which of the cars {@carIds} is plugged into it",
            connectorId, probeCurrent, candidates.Select(c => c.Id));
        var wasCharging = connectorState.IsCharging.Value;
        var lastSetCurrent = connectorState.LastSetCurrent.Value;
        var startResult = await ocppChargePointActionService
            .StartCharging(connectorId, probeCurrent, null, cancellationToken).ConfigureAwait(false);
        if (startResult.HasError)
        {
            logger.LogWarning("Could not offer probe current on connector {connectorId}: {error}", connectorId, startResult.ErrorMessage);
            return null;
        }
        var matchedCarId = await WaitForReaction(candidates, probeCurrent, cancellationToken).ConfigureAwait(false);
        if (matchedCarId == default)
        {
            logger.LogTrace("No single car reacted to the probe of connector {connectorId}", connectorId);
            await RestoreConnector(connectorId, wasCharging, lastSetCurrent, cancellationToken).ConfigureAwait(false);
            return null;
        }
        logger.LogTrace("Car {carId} is plugged into connector {connectorId}", matchedCarId, connectorId);
        if (wasCharging)
        {
            await RestoreConnector(connectorId, wasCharging, lastSetCurrent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await HandOverToCar(connectorId, maxCurrent, matchedCarId.Value, cancellationToken).ConfigureAwait(false);
        }
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
        await WaitUntil(candidates, () => candidates.Any(c => c.ChargerPilotCurrent.Value == probeCurrent), cancellationToken)
            .ConfigureAwait(false);
        var reactedCars = candidates.Where(c => c.ChargerPilotCurrent.Value == probeCurrent).ToList();
        if (reactedCars.Count > 1)
        {
            logger.LogWarning("Cars {@carIds} all report the probe current of {probeCurrent} A, can not decide which one is plugged in",
                reactedCars.Select(c => c.Id), probeCurrent);
            return null;
        }
        return reactedCars.SingleOrDefault()?.Id;
    }

    /// <returns>Whether the condition was met before the wait timed out.</returns>
    private async Task<bool> WaitUntil(List<DtoCar> cars, Func<bool> condition, CancellationToken cancellationToken)
    {
        for (var poll = 1; poll <= MaxPolls; poll++)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            if (poll % PollsBetweenBleRefreshes == 0)
            {
                foreach (var car in cars)
                {
                    //Does nothing for cars that do not collect their data via BLE.
                    await bleVehicleDataService.RefreshSingleCarData(car.Id).ConfigureAwait(false);
                }
            }
            if (condition())
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Leaves the connector the way the charging cycle leaves it for a Tesla it stopped: the car is stopped via the
    /// Tesla API or BLE and the connector offers its max current, so the car can use grid power e.g. for preconditioning.
    /// </summary>
    private async Task HandOverToCar(int connectorId, int maxCurrent, int carId, CancellationToken cancellationToken)
    {
        await teslaService.StopCharging(carId).ConfigureAwait(false);
        var car = settings.Cars.First(c => c.Id == carId);
        //The stop command does not report whether the car stopped, and opening the connector before it did lets the
        //car charge with the max current.
        if (!await WaitUntil([car], () => car.IsCharging.Value != true, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Car {carId} did not stop charging after the probe of connector {connectorId}, stop the connector instead", carId, connectorId);
            await RestoreConnector(connectorId, false, null, cancellationToken).ConfigureAwait(false);
            return;
        }
        var startResult = await ocppChargePointActionService
            .StartCharging(connectorId, maxCurrent, null, cancellationToken).ConfigureAwait(false);
        if (startResult.HasError)
        {
            logger.LogWarning("Could not set connector {connectorId} to its max current of {maxCurrent} A: {error}", connectorId, maxCurrent, startResult.ErrorMessage);
        }
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
