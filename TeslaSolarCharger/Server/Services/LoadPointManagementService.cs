using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services;

public class LoadPointManagementService : ILoadPointManagementService
{
    private readonly ILogger<LoadPointManagementService> _logger;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;
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
    ISettings settings,
    IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _errorHandlingService = errorHandlingService;
        _issueKeys = issueKeys;
        _context = context;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Get all load points with their charging power and voltage details. Lightweight method without database calls.
    /// </summary>
    /// <returns></returns>
    public List<DtoLoadPointWithCurrentChargingValues> GetLoadPointsWithChargingDetails()
    {
        _logger.LogTrace("{method}()", nameof(GetLoadPointsWithChargingDetails));
        var cars = _settings.Cars
            .Where(c => c.ChargingPowerAtHome > 0)
            .Select(c => new
            {
                CarId = c.Id,
                ChargingPower = c.ChargingPowerAtHome ?? 0,
                Voltage = c.ChargerVoltage ?? _settings.AverageHomeGridVoltage ?? 230,
                ChargingCurrent = c.IsHomeGeofence == true ? (c.ChargerActualCurrent ?? 0) : 0,
                Phases = c.ActualPhases,
            })
            .ToList();
        var connectors = _settings.OcppConnectorStates
            .Where(c => c.Value.ChargingPower.Value > 0)
            .Select(c => new
            {
                ConnectorId = c.Key,
                ChargingPower = c.Value.ChargingPower.Value,
                Voltage = (int)c.Value.ChargingVoltage.Value,
                ChargingCurrent = c.Value.ChargingCurrent.Value,
                Phases = c.Value.PhaseCount.Value,
            })
            .ToList();

        var matches = GetCarConnectorMatches(cars.Select(c => c.CarId), connectors.Select(c => c.ConnectorId));
        var result = new List<DtoLoadPointWithCurrentChargingValues>();
        foreach (var match in matches)
        {
            var loadPoint = new DtoLoadPointWithCurrentChargingValues
            {
                CarId = match.CarId,
                ChargingConnectorId = match.ConnectorId,
            };
            if (match.CarId != default)
            {
                var car = cars.First(c => c.CarId == match.CarId.Value);
                loadPoint.ChargingPower = car.ChargingPower;
                loadPoint.ChargingVoltage = car.Voltage;
                loadPoint.ChargingCurrent = car.ChargingCurrent;
                loadPoint.ChargingPhases = car.Phases;
            }
            if (match.ConnectorId != default)
            {
                var connector = connectors.First(c => c.ConnectorId == match.ConnectorId.Value);
                loadPoint.ChargingPower = connector.ChargingPower;
                loadPoint.ChargingVoltage = connector.Voltage;
                loadPoint.ChargingCurrent = connector.ChargingCurrent;
                loadPoint.ChargingPhases = connector.Phases;
            }
            result.Add(loadPoint);
        }
        return result;
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
                loadPoint.ChargingPower = dtoCar.ChargingPowerAtHome;
                loadPoint.ChargingPriority = dtoCar.ChargingPriority;
                loadPoint.IsHome = dtoCar.IsHomeGeofence;
                loadPoint.IsPluggedIn = dtoCar.PluggedIn == true;
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
                if ((loadPoint.ChargingPriority == null) || (loadPoint.ChargingPriority > databaseConnector.ChargingPriority))
                {
                    loadPoint.ChargingPriority = databaseConnector.ChargingPriority;
                }

                var connectorState = _settings.OcppConnectorStates.GetValueOrDefault(pair.ConnectorId.Value);
                if (connectorState != default)
                {
                    loadPoint.ActualCurrent = connectorState.ChargingCurrent.Value;
                    loadPoint.ActualPhases = connectorState.PhaseCount.Value;
                    loadPoint.ChargingPower = connectorState.ChargingPower.Value;
                    loadPoint.IsPluggedIn = connectorState.IsPluggedIn.Value;
                    //Charging connectors are always home connectors
                    loadPoint.IsHome = true;
                }
            }
            result.Add(loadPoint);
        }

        return result.OrderBy(l => l.ChargingPriority ?? 99).ToList();
    }

    public HashSet<(int? CarId, int? ConnectorId)> GetCarConnectorMatches(IEnumerable<int> carIds, IEnumerable<int> connectorIds)
    {
        _logger.LogTrace("{methdod}({@carId}, {@connectorIds})", nameof(GetCarConnectorMatches), carIds, connectorIds);
        var matches = new HashSet<(int? CarId, int? ConnectorId)>();
        var errorForMultipleMatches = false;
        var maxTimeDiff = _configurationWrapper.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector();

        foreach (var connectorId in connectorIds)
        {
            var state = _settings.OcppConnectorStates.GetValueOrDefault(connectorId);
            if (_settings.ManualSetLoadPointCarCombinations.TryGetValue(connectorId, out var value))
            {
                if (value.combinationTimeStamp >= state?.IsPluggedIn.LastChanged)
                {
                    matches.Add((value.carId, connectorId));
                    continue;
                }
            }
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

    public void UpdateChargingConnectorCar(int chargingConnectorId, int? carId)
    {
        _logger.LogTrace("{method}({chargingConnectorId}, {carId})", nameof(UpdateChargingConnectorCar), chargingConnectorId, carId);
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        if (carId != default)
        {
            var car = _settings.Cars.First(c => c.Id == carId.Value);
            if (car.PluggedIn != true)
            {
                throw new InvalidOperationException("Car is not plugged in, therefore it can not be set as car for charging connector.");
            }
        }
        _settings.ManualSetLoadPointCarCombinations[chargingConnectorId] = (carId, currentDate);
    }
}
