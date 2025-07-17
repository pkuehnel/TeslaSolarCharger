using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Services;

public class LoadPointManagementService : ILoadPointManagementService
{
    private readonly ILogger<LoadPointManagementService> _logger;
    private readonly ISettings _settings;
    private readonly IAppStateNotifier _appStateNotifier;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IChangeTrackingService _changeTrackingService;
    private readonly IEntityKeyGenerationHelper _entityKeyGenerationHelper;
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
    IAppStateNotifier appStateNotifier,
    IDateTimeProvider dateTimeProvider,
    IChangeTrackingService changeTrackingService,
    IEntityKeyGenerationHelper entityKeyGenerationHelper)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _errorHandlingService = errorHandlingService;
        _issueKeys = issueKeys;
        _context = context;
        _settings = settings;
        _appStateNotifier = appStateNotifier;
        _dateTimeProvider = dateTimeProvider;
        _changeTrackingService = changeTrackingService;
        _entityKeyGenerationHelper = entityKeyGenerationHelper;
    }

    public async Task OcppStateChanged(int chargingConnectorId)
    {
        _logger.LogTrace("{method}({chargingConnectorId})", nameof(OcppStateChanged), chargingConnectorId);
        var loadpoint = _settings.LatestLoadPointCombinations
            .FirstOrDefault(l => l.ChargingConnectorId == chargingConnectorId);
        if (loadpoint == default)
        {
            _logger.LogWarning("No loadpoint with charging connector {chargingConnector} found, do not send updates to client", chargingConnectorId);
            return;
        }

        await NotifyClientsForChangedLoadpointValues(loadpoint);
    }

    public async Task CarStateChanged(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(CarStateChanged), carId);
        var loadpoint = _settings.LatestLoadPointCombinations
            .FirstOrDefault(l => l.CarId == carId);
        if (loadpoint != default)
        {
            await NotifyClientsForChangedLoadpointValues(loadpoint);
        }
        var car = _settings.Cars.FirstOrDefault(c => c.Id == carId);
        if (car != default)
        {
            var carState = new DtoCarOverviewState()
            {
                CarSideSocLimit = car.SocLimit,
                IsCharging = car.State == CarStateEnum.Charging,
                IsHome = car.IsHomeGeofence == true,
                IsPluggedIn = car.PluggedIn == true,
                Soc = car.SoC,
            };
            await NotifyClientsForChangedCarValues(carId, carState).ConfigureAwait(false);
        }
        
    }

    public async Task<HashSet<DtoLoadpointCombination>> GetCombinationsToManage()
    {
        _logger.LogTrace("{method}()", nameof(GetCombinationsToManage));
        var carIdsToManage = await _context.Cars
            .Where(c => c.ShouldBeManaged == true)
            .Select(c => c.Id)
            .ToHashSetAsync().ConfigureAwait(false);
        var connectorIdsToManage = await _context.OcppChargingStationConnectors
            .Where(c => c.ShouldBeManaged)
            .Select(c => c.Id)
            .ToHashSetAsync().ConfigureAwait(false);
        var combinations = await GetCarConnectorMatches(carIdsToManage, connectorIdsToManage, true);
        return combinations;
    }

    private async Task NotifyClientsForChangedCarValues(int carId, DtoCarOverviewState carState)
    {
        _logger.LogTrace("{method}({@carState})", nameof(NotifyClientsForChangedCarValues), carState);
        var changes = _changeTrackingService.DetectChanges(
            DataTypeConstants.CarOverviewState,
            carId.ToString(),
            carState);
        if (changes != default)
        {
            await _appStateNotifier.NotifyStateUpdateAsync(changes).ConfigureAwait(false);
        }
    }

    private async Task NotifyClientsForChangedLoadpointValues(DtoLoadpointCombination loadpoint)
    {
        _logger.LogTrace("{method}({@loadpoint})", nameof(NotifyClientsForChangedLoadpointValues), loadpoint);
        var loadpointWithChargingValues = GetLoadPointWithChargingValues(loadpoint);
        var changes = _changeTrackingService.DetectChanges(
            DataTypeConstants.LoadPointOverviewValues,
            _entityKeyGenerationHelper.GetLoadPointEntityKey(loadpoint.CarId, loadpoint.ChargingConnectorId),
            loadpointWithChargingValues);
        if (changes != default)
        {
            await _appStateNotifier.NotifyStateUpdateAsync(changes).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Get all load points with their charging power and voltage details. Lightweight method without database calls.
    /// </summary>
    /// <returns></returns>
    public async Task<List<DtoLoadPointWithCurrentChargingValues>> GetLoadPointsWithChargingDetails()
    {
        _logger.LogTrace("{method}()", nameof(GetLoadPointsWithChargingDetails));
        var carIds = _settings.Cars
            .Where(c => c.ChargingPowerAtHome > 0)
            .Select(c => c.Id)
            .ToHashSet();
        var connectorIds = _settings.OcppConnectorStates
            .Where(c => c.Value.ChargingPower.Value > 0)
            .Select(c => c.Key)
            .ToList();

        var matches = await GetCarConnectorMatches(carIds, connectorIds, false).ConfigureAwait(false);
        var result = new List<DtoLoadPointWithCurrentChargingValues>();
        foreach (var match in matches)
        {
            var loadPoint = GetLoadPointWithChargingValues(match);
            result.Add(loadPoint);
        }
        return result;
    }

    public DtoLoadPointWithCurrentChargingValues GetLoadPointWithChargingValues(DtoLoadpointCombination match)
    {
        var loadPoint = new DtoLoadPointWithCurrentChargingValues
        {
            CarId = match.CarId,
            ChargingConnectorId = match.ChargingConnectorId,
        };
        if (match.CarId != default)
        {
            var car = _settings.Cars.First(c => c.Id == match.CarId.Value);
            loadPoint.ChargingPower = car.ChargingPowerAtHome ?? 0;
            loadPoint.ChargingVoltage = car.ChargerVoltage ?? _settings.AverageHomeGridVoltage ?? 230;
            loadPoint.ChargingCurrent = car.IsHomeGeofence == true ? (car.ChargerActualCurrent ?? 0) : 0;
            loadPoint.ChargingPhases = car.ActualPhases;
        }
        if (match.ChargingConnectorId != default)
        {
            if (_settings.OcppConnectorStates.TryGetValue(match.ChargingConnectorId.Value, out var connector))
            {
                loadPoint.ChargingPower = connector.ChargingPower.Value;
                loadPoint.ChargingVoltage = (int)connector.ChargingVoltage.Value;
                loadPoint.ChargingCurrent = connector.ChargingCurrent.Value;
                loadPoint.ChargingPhases = connector.PhaseCount.Value;
            }
        }

        return loadPoint;
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
        var connectorPairs = await GetCarConnectorMatches(carData.Select(c => c.Id), connectorData.Select(c => c.Id), true).ConfigureAwait(false);
        var result = new List<DtoLoadPointOverview>();
        foreach (var pair in connectorPairs)
        {
            var loadPoint = new DtoLoadPointOverview()
            {
                CarId = pair.CarId,
                ChargingConnectorId = pair.ChargingConnectorId,
            };
            if (pair.CarId != default)
            {
                var databaseCar = carData.First(c => c.Id == pair.CarId);
                loadPoint.MaxCurrent = databaseCar.MaxCurrent;
                loadPoint.MinCurrent = databaseCar.MinCurrent;

                var dtoCar = _settings.Cars.First(c => c.Id == pair.CarId.Value);
                loadPoint.ActualCurrent = dtoCar.ChargerActualCurrent;
                loadPoint.ActualPhases = dtoCar.ActualPhases;
                loadPoint.MaxPhases = dtoCar.ActualPhases;
                loadPoint.ChargingPower = dtoCar.ChargingPowerAtHome;
                loadPoint.ChargingPriority = dtoCar.ChargingPriority;
                loadPoint.IsHome = dtoCar.IsHomeGeofence;
                loadPoint.IsPluggedIn = dtoCar.PluggedIn == true;
                loadPoint.EstimatedVoltageWhileCharging = CalculateEstimatedChargerVoltageWhileCharging(dtoCar.ChargerVoltage);
                //Currently always true as all cars are Teslas
                loadPoint.ManageChargingPowerByCar = true;
            }

            if (pair.ChargingConnectorId != default)
            {
                var databaseConnector = connectorData.First(c => c.Id == pair.ChargingConnectorId);
                if ((loadPoint.MaxCurrent == null) || (loadPoint.MaxCurrent > databaseConnector.MaxCurrent))
                {
                    loadPoint.MaxCurrent = databaseConnector.MaxCurrent;
                }
                if ((loadPoint.MinCurrent == null) || (loadPoint.MinCurrent < databaseConnector.MinCurrent))
                {
                    loadPoint.MinCurrent = databaseConnector.MinCurrent;
                }
                if ((loadPoint.MaxPhases == null) || (loadPoint.MaxPhases < databaseConnector.MaxPhases))
                {
                    loadPoint.MaxPhases = databaseConnector.MaxPhases;
                }
                if ((loadPoint.ChargingPriority == null) || (loadPoint.ChargingPriority > databaseConnector.ChargingPriority))
                {
                    loadPoint.ChargingPriority = databaseConnector.ChargingPriority;
                }

                var connectorState = _settings.OcppConnectorStates.GetValueOrDefault(pair.ChargingConnectorId.Value);
                if (connectorState != default)
                {
                    loadPoint.ActualCurrent = connectorState.ChargingCurrent.Value;
                    loadPoint.ActualPhases = connectorState.PhaseCount.Value;
                    loadPoint.ChargingPower = connectorState.ChargingPower.Value;
                    loadPoint.IsPluggedIn = connectorState.IsPluggedIn.Value;
                    loadPoint.EstimatedVoltageWhileCharging = CalculateEstimatedChargerVoltageWhileCharging((int?)connectorState.ChargingVoltage.Value);
                    //Charging connectors are always home connectors
                    loadPoint.IsHome = true;
                }
            }
            result.Add(loadPoint);
        }

        return result.OrderBy(l => l.ChargingPriority ?? 99).ToList();
    }

    public async Task<HashSet<DtoLoadpointCombination>> GetCarConnectorMatches(IEnumerable<int> carIds, IEnumerable<int> connectorIds, bool updateSettingsMatches)
    {
        _logger.LogTrace("{methdod}({@carId}, {@connectorIds}, {updateSettingsMatches})", nameof(GetCarConnectorMatches), carIds, connectorIds, updateSettingsMatches);
        var matches = new HashSet<DtoLoadpointCombination>();
        var errorForMultipleMatches = false;
        var maxTimeDiff = _configurationWrapper.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector();

        var plugInRelevantCarData = _settings.Cars.Where(c => carIds.Contains(c.Id)).Select(
            c => new
            {
                c.Id,
                c.LastPluggedIn,
                c.PluggedIn,
                c.IsHomeGeofence,
            }).ToList();
        _logger.LogTrace("Logging states of carIds.");
        foreach (var plugInRelevantCarDatum in plugInRelevantCarData)
        {
            _logger.LogTrace("{@carDatum}", plugInRelevantCarDatum);
        }

        foreach (var connectorId in connectorIds)
        {
            if(!_settings.OcppConnectorStates.TryGetValue(connectorId, out var connectorState))
            {
                matches.Add(new(null, connectorId));
                continue;
            }

            if (_settings.ManualSetLoadPointCarCombinations.TryGetValue(connectorId, out var value))
            {
                _logger.LogDebug("Found match in {settings}.{manualSetCombinations} for connector {connectorId}", nameof(_settings), nameof(_settings.ManualSetLoadPointCarCombinations), connectorId);
                var matchValid = true;
                var carId = value.carId;
                if (carId != default)
                {
                    _logger.LogTrace("Found car {carId} for connector {connectorId} in manual set combinations", carId, connectorId);
                    var car = _settings.Cars.First(c => c.Id == carId.Value);
                    if (car.PluggedIn != true)
                    {
                        _logger.LogDebug("Car {carId} is not plugged in, therefore it can not be set as car for charging connector {connectorId}.", carId, connectorId);
                        matchValid = false;
                    }

                    if (car.LastPluggedIn > value.combinationTimeStamp)
                    {
                        _logger.LogDebug("Car {carId} changed plugged in state since setup of manual car combination", carId);
                        matchValid = false;
                    }
                }
                _logger.LogTrace("Match valid: {matchValid}; combinationTimeStamp: {combinationTimeStamp}; lastPluggedInChange: {lastPluggedInChange}; plugged in state: {pluggedIn}",
                    matchValid, value.combinationTimeStamp, connectorState.IsPluggedIn.LastChanged, connectorState.IsPluggedIn.Value);
                if (matchValid
                        && (value.combinationTimeStamp >= connectorState.IsPluggedIn.LastChanged)
                        && connectorState.IsPluggedIn.Value)
                {
                    _logger.LogDebug("Car {carId} is valid for connector {connectorId} in manual set combinations", value.carId, connectorId);
                    matches.Add(new(value.carId, connectorId));
                    continue;
                }
            }


            if (connectorState.IsPluggedIn.LastChanged == default)
            {
                matches.Add(new(null, connectorId));
                continue;
            }

            var matchWindowStart = connectorState.IsPluggedIn.LastChanged.Value.Add(-maxTimeDiff);
            var matchWindowEnd = connectorState.IsPluggedIn.LastChanged.Value.Add(maxTimeDiff);
            _logger.LogTrace("Charging Connector {chargingConnectorId} match window start {matchWindowStart}, {matchWindowEnd}",
                connectorId, matchWindowStart, matchWindowEnd);

            var matchingCars = plugInRelevantCarData
                .Where(car => car.LastPluggedIn >= matchWindowStart
                              && car.LastPluggedIn <= matchWindowEnd
                              && car.IsHomeGeofence == true)
                .ToList();

            if (matchingCars.Count == 1)
            {
                var carId = matchingCars.First().Id;
                _logger.LogTrace("Found car match for {connectorId}: {carId}", connectorId, carId);
                matches.Add(new(carId, connectorId));
            }
            else if (matchingCars.Count > 1)
            {
                _logger.LogTrace("Found car match for {connectorId}: {@cars}", connectorId, matchingCars);
                errorForMultipleMatches = true;
            }
            else
            {
                _logger.LogTrace("Found no car match for {connectorId}.", connectorId);
                matches.Add(new(null, connectorId));
            }
        }

        foreach (var carId in carIds)
        {
            if (!matches.Any(c => c.CarId == carId))
            {
                _logger.LogTrace("Add additional loadpoint for car {carId} as did not match to any charging connector", carId);
                matches.Add(new(carId, null));
            }
        }

        if (errorForMultipleMatches)
        {
            var waitSeconds = _configurationWrapper.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector().TotalSeconds * 3;
            await _errorHandlingService.HandleError(
                nameof(LoadPointManagementService),
                nameof(GetCarConnectorMatches),
                "Could not autodetect cars plugged in charging points",
                $"Multiple cars matched to a single charging connector. Unplug all but one car and wait {waitSeconds} seconds between each plugin.",
                _issueKeys.MultipleCarsMatchChargingConnector,
                null,
                null).ConfigureAwait(false);
        }
        else
        {
            await _errorHandlingService.HandleErrorResolved(_issueKeys.MultipleCarsMatchChargingConnector, null).ConfigureAwait(false);
        }

        if (updateSettingsMatches && (!_settings.LatestLoadPointCombinations.SetEquals(matches)))
        {
            var changes = new StateUpdateDto()
            {
                DataType = DataTypeConstants.LoadPointMatchesChangeTrigger,
                Timestamp = _dateTimeProvider.DateTimeOffSetUtcNow(),
            };
            await _appStateNotifier.NotifyStateUpdateAsync(changes).ConfigureAwait(false);
            _settings.LatestLoadPointCombinations = matches.ToHashSet();
        }
        return matches;
    }

    public void UpdateChargingConnectorCar(int chargingConnectorId, int? carId)
    {
        _logger.LogTrace("{method}({chargingConnectorId}, {carId})", nameof(UpdateChargingConnectorCar), chargingConnectorId, carId);
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        if (_settings.OcppConnectorStates.TryGetValue(chargingConnectorId, out var state))
        {
            if (!state.IsPluggedIn.Value)
            {
                throw new InvalidOperationException("Connector is not plugged in, therefore no car can be set");
            }
        }
        else
        {
            throw new InvalidOperationException($"Charging connector is not connected via OCPP");
        }
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

    private int CalculateEstimatedChargerVoltageWhileCharging(int? actualVoltage)
    {
        _logger.LogTrace("{method}({actualVoltage})", nameof(CalculateEstimatedChargerVoltageWhileCharging), actualVoltage);
        if (actualVoltage == default || actualVoltage <= 70)
        {
            return _settings.AverageHomeGridVoltage ?? 230;
        }
        return actualVoltage.Value;
    }
}
