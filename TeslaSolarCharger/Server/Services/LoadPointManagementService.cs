using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
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

    public async Task<HashSet<(int? carId, int? connectorId)>> GetLoadPointsToManage()
    {
        _logger.LogTrace("{method}()", nameof(GetLoadPointsToManage));
        var carIds = await _context.Cars
            .Where(c => c.ShouldBeManaged == true)
            .Select(c => c.Id)
            .ToHashSetAsync();
        var connectorIds = await _context.OcppChargingStationConnectors
            .Where(c => c.ShouldBeManaged)
            .Select(c => c.Id)
            .ToHashSetAsync();
        var connectorPairs = GetCarConnectorMatches(carIds, connectorIds);
        return connectorPairs;
    }

    private HashSet<(int? CarId, int? ConnectorId)> GetCarConnectorMatches(
        HashSet<int> carIds,
        HashSet<int> connectorIds)
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
                .Where(car => car.LastPluggedIn >= matchWindowStart && car.LastPluggedIn <= matchWindowEnd)
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
