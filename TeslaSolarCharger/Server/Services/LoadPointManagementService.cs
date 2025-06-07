using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class LoadPointManagementService : ILoadPointManagementService
{
    private readonly ILogger<LoadPointManagementService> _logger;
    private readonly ISettings _settings;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly IIssueKeys _issueKeys;

    public LoadPointManagementService(
    ILogger<LoadPointManagementService> logger,
    IConfigurationWrapper configurationWrapper,
    IErrorHandlingService errorHandlingService,
    IIssueKeys issueKeys,
    ITeslaSolarChargerContext context,
    ISettings settings)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _errorHandlingService = errorHandlingService;
        _issueKeys = issueKeys;
        _context = context;
        _settings = settings;
    }

    public async Task<List<DtoLoadpoint>> GetPluggedInLoadPoints()
    {
        _logger.LogTrace("{Method}()", nameof(GetPluggedInLoadPoints));

        var homePluggedInCarIds = _settings.Cars
            .Where(car => car.IsHomeGeofence == true
                          && car.PluggedIn == true
                          && car.ShouldBeManaged == true)
            .Select(c => c.Id)
            .ToHashSet();

        var pluggedInConnectorIds = _settings.OcppConnectorStates
            .Where(connectorState => connectorState.Value.IsPluggedIn.Value)
            .Select(connector => connector.Key)
            .ToHashSet();

        var shouldNotBeManagedChargingConnectorIds = await _context.OcppChargingStationConnectors
            .Where(c => c.ShouldBeManaged == false)
            .Select(c => c.Id)
            .ToHashSetAsync();
        foreach (var notBeManagedConnectorId in shouldNotBeManagedChargingConnectorIds)
        {
            pluggedInConnectorIds.Remove(notBeManagedConnectorId);
        }

        var carConnectorPairs = GetCarConnectorMatches(homePluggedInCarIds, pluggedInConnectorIds);

        var loadPoints = CreateLoadPointsFromMatches(carConnectorPairs);

        await ApplyConnectorPrioritiesAsync(loadPoints).ConfigureAwait(false);

        return loadPoints
            .OrderBy(lp => lp.Priority)
            .ToList();
    }

    public async Task<List<DtoLoadPointOverview>> GetLoadPointsToManage()
    {
        _logger.LogTrace("{method}()", nameof(GetLoadPointsToManage));
        var carData = await _context.Cars
            .Where(c => c.ShouldBeManaged == true)
            .Select(c => new
            {
                c.Id,
                MinCurrent = c.MinimumAmpere,
                MaxCurrent = c.MaximumAmpere,
            })
            .ToHashSetAsync();
        var connectorData = await _context.OcppChargingStationConnectors
            .Where(c => c.ShouldBeManaged)
            .Select(c => new
            {
                c.Id,
                c.MinCurrent,
                c.MaxCurrent,
                MaxPhases = c.ConnectedPhasesCount,
                c.ChargingPriority,
            })
            .ToHashSetAsync();
        var connectorPairs = GetCarConnectorMatches(carData.Select(c => c.Id), connectorData.Select(c => c.Id));
        var result = new List<DtoLoadPointOverview>();
        foreach (var pair in connectorPairs)
        {
            var loadPoint = new DtoLoadPointOverview()
            {
                CarId = pair.CarId,
                ChargingConnectorId = pair.ConnectorId,
            };
            if (pair.CarId != default)
            {
                var databaseCar = carData.First(c => c.Id == pair.CarId);
                loadPoint.MaxCurrent = databaseCar.MaxCurrent;

                var dtoCar = _settings.Cars.First(c => c.Id == pair.CarId.Value);
                loadPoint.ActualCurrent = dtoCar.ChargerActualCurrent;
                loadPoint.ActualPhases = dtoCar.ActualPhases;
                loadPoint.MaxPhases = dtoCar.ActualPhases;
            }

            if (pair.ConnectorId != default)
            {
                var databaseConnector = connectorData.First(c => c.Id == pair.ConnectorId);
                if ((loadPoint.MaxCurrent == null) || (loadPoint.MaxCurrent > databaseConnector.MaxCurrent))
                {
                    loadPoint.MaxCurrent = databaseConnector.MaxCurrent;
                }
                if ((loadPoint.MaxPhases == null) || (loadPoint.MaxPhases < databaseConnector.MaxPhases))
                {
                    loadPoint.MaxPhases = databaseConnector.MaxPhases;
                }

                var connectorState = _settings.OcppConnectorStates.GetValueOrDefault(pair.ConnectorId.Value);
                if (connectorState != default)
                {
                    loadPoint.ActualCurrent = connectorState.ChargingCurrent.Value;
                    loadPoint.ActualPhases = connectorState.PhaseCount.Value;
                    loadPoint.ChargingPower = connectorState.ChargingPower.Value;
                }
            }
            result.Add(loadPoint);
        }

        return result;
    }

    private HashSet<(int? CarId, int? ConnectorId)> GetCarConnectorMatches(
        IEnumerable<int> carIds,
        IEnumerable<int> connectorIds)
    {
        var matches = new HashSet<(int? CarId, int? ConnectorId)>();
        var errorForMultipleMatches = false;
        var maxTimeDiff = _configurationWrapper.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector();

        foreach (var connectorId in connectorIds)
        {
            var state = _settings.OcppConnectorStates.GetValueOrDefault(connectorId);
            if (state == default)
            {
                matches.Add((null, connectorId));
                continue;
            }
            if (state.IsPluggedIn.LastChanged == default)
            {
                matches.Add((null, connectorId));
                continue;
            }

            var matchWindowStart = state.IsPluggedIn.LastChanged.Value.Add(-maxTimeDiff);
            var matchWindowEnd = state.IsPluggedIn.LastChanged.Value.Add(maxTimeDiff);

            var matchingCars = _settings.Cars
                .Where(car => car.LastPluggedIn >= matchWindowStart
                              && car.LastPluggedIn <= matchWindowEnd
                              && car.IsHomeGeofence == true)
                .ToList();

            if (matchingCars.Count == 1)
            {
                matches.Add((matchingCars.First().Id, connectorId));
            }
            else if (matchingCars.Count > 1)
            {
                errorForMultipleMatches = true;
            }
            else
            {
                matches.Add((null, connectorId));
            }
        }

        foreach (var carId in carIds)
        {
            if (!matches.Any(c => c.CarId == carId))
            {
                matches.Add((carId, null));
            }
        }

        if (errorForMultipleMatches)
        {
            var waitSeconds = _configurationWrapper.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector().TotalSeconds * 3;
            _errorHandlingService.HandleError(
                nameof(LoadPointManagementService),
                nameof(GetCarConnectorMatches),
                "Could not autodetect cars plugged in charging points",
                $"Multiple cars matched to a single charging connector. Unplug all but one car and wait {waitSeconds} seconds between each plugin.",
                _issueKeys.MultipleCarsMatchChargingConnector,
                null,
                null);
        }
        else
        {
            _errorHandlingService.HandleErrorResolved(_issueKeys.MultipleCarsMatchChargingConnector, null);
        }

        return matches;
    }

    private List<DtoLoadpoint> CreateLoadPointsFromMatches(HashSet<(int? CarId, int? ConnectorId)> pairs)
    {
        var result = new List<DtoLoadpoint>();
        foreach (var (carId, connectorId) in pairs)
        {
            var loadPoint = new DtoLoadpoint
            {
                OcppConnectorId = connectorId,
            };
            if (carId != default)
            {
                loadPoint.Car = _settings.Cars.First(c => c.Id == carId);
            }
            if (connectorId != default && _settings.OcppConnectorStates.TryGetValue(connectorId.Value, out var state))
            {
                loadPoint.OcppConnectorState = state;
            }
            result.Add(loadPoint);
        }

        return result;
    }

    private async Task ApplyConnectorPrioritiesAsync(List<DtoLoadpoint> loadPoints)
    {
        var connectorIds = loadPoints
            .Where(lp => lp.OcppConnectorId != default)
            .Select(lp => lp.OcppConnectorId!.Value)
            .ToHashSet();

        var priorities = await _context.OcppChargingStationConnectors
            .Where(conn => connectorIds.Contains(conn.Id))
            .ToDictionaryAsync(conn => conn.Id, conn => conn.ChargingPriority)
            .ConfigureAwait(false);

        foreach (var loadPoint in loadPoints)
        {
            var carPriority = loadPoint.Car?.ChargingPriority;
            if (carPriority == default)
            {
                loadPoint.Priority = priorities.GetValueOrDefault(loadPoint.OcppConnectorId ?? 0, 99);
            }
            else
            {
                loadPoint.Priority = carPriority.Value;
            }
        }
    }
}
