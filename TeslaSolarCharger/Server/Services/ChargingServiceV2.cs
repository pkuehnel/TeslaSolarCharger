using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services;

public class ChargingServiceV2 : IChargingServiceV2
{
    private readonly ILogger<ChargingServiceV2> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ILoadPointManagementService _loadPointManagementService;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOcppChargePointActionService _ocppChargePointActionService;
    private readonly ISettings _settings;
    private readonly ITscOnlyChargingCostService _tscOnlyChargingCostService;
    private readonly IEnergyDataService _energyDataService;
    private readonly ISunCalculator _sunCalculator;
    private readonly IHomeBatteryEnergyCalculator _homeBatteryEnergyCalculator;
    private readonly IConstants _constants;
    private readonly ITeslaService _teslaService;

    public ChargingServiceV2(ILogger<ChargingServiceV2> logger,
        IConfigurationWrapper configurationWrapper,
        ILoadPointManagementService loadPointManagementService,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        IOcppChargePointActionService ocppChargePointActionService,
        ISettings settings,
        ITscOnlyChargingCostService tscOnlyChargingCostService,
        IEnergyDataService energyDataService,
        ISunCalculator sunCalculator,
        IHomeBatteryEnergyCalculator homeBatteryEnergyCalculator,
        IConstants constants,
        ITeslaService teslaService)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _loadPointManagementService = loadPointManagementService;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _ocppChargePointActionService = ocppChargePointActionService;
        _settings = settings;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
        _energyDataService = energyDataService;
        _sunCalculator = sunCalculator;
        _homeBatteryEnergyCalculator = homeBatteryEnergyCalculator;
        _constants = constants;
        _teslaService = teslaService;
    }

    public async Task SetNewChargingValues(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(SetNewChargingValues));
        if (!_configurationWrapper.UseChargingServiceV2())
        {
            return;
        }
        await CalculateGeofences();
        var loadPoints = await _loadPointManagementService.GetPluggedInLoadPoints();
        var powerToControl = await CalculatePowerToControl(loadPoints.Select(l => l.ActualChargingPower ?? 0).Sum(), cancellationToken).ConfigureAwait(false);
        await UpdateShouldStartStopChargingTimes(powerToControl);
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var chargingSchedules = new List<DtoChargingSchedule>();
        foreach (var dtoLoadpoint in loadPoints)
        {
            if (dtoLoadpoint.Car != default)
            {
                var (carUsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent) = await GetChargingScheduleRelevantData(dtoLoadpoint.Car.Id, dtoLoadpoint.OcppConnectorId).ConfigureAwait(false);
                if (dtoLoadpoint.Car.MinimumSoC > dtoLoadpoint.Car.SoC)
                {
                    var earliestPossibleChargingSchedule =
                        GenerateEarliestOrLatestPossibleChargingSchedule(dtoLoadpoint.Car.MinimumSoC, null,
                            carUsableEnergy, carSoC, maxPhases, maxCurrent, dtoLoadpoint.Car.Id, dtoLoadpoint.OcppConnectorId);
                    if (earliestPossibleChargingSchedule != default)
                    {
                        chargingSchedules.Add(earliestPossibleChargingSchedule);
                        //Do not plan anything else, before min Soc is reached
                        continue;
                    }
                }
                var nextTarget = await GetNextTarget(dtoLoadpoint.Car.Id, cancellationToken).ConfigureAwait(false);
                if (nextTarget != default)
                {
                    var latestPossibleChargingSchedule =
                        GenerateEarliestOrLatestPossibleChargingSchedule(nextTarget.TargetSoc, nextTarget.NextExecutionTime,
                            carUsableEnergy, carSoC, maxPhases, maxCurrent, dtoLoadpoint.Car.Id, dtoLoadpoint.OcppConnectorId);
                    if (latestPossibleChargingSchedule != default)
                    {
                        chargingSchedules.Add(latestPossibleChargingSchedule);
                    }
                    var gridPrices = await _tscOnlyChargingCostService.GetPricesInTimeSpan(currentDate, nextTarget.NextExecutionTime);

                }
            }
        }

        _logger.LogDebug("Final calculated power to control: {powerToControl}", powerToControl);
        var alreadyControlledLoadPoints = new HashSet<(int? carId, int? connectorId)>();
        currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var activeChargingSchedules = chargingSchedules.Where(s => s.StartTime <= currentDate).ToList();

        var maxAdditionalCurrent = _configurationWrapper.MaxCombinedCurrent() - loadPoints.Select(l => l.ActualCurrent ?? 0).Sum();
        foreach (var activeChargingSchedule in activeChargingSchedules)
        {
            var correspondingLoadPoint = loadPoints.FirstOrDefault(l => l.Car?.Id == activeChargingSchedule.CarId && l.OcppConnectorId == activeChargingSchedule.OccpChargingConnectorId);
            if (correspondingLoadPoint == default)
            {
                _logger.LogWarning("No loadpoint found for car {carId} and connector {connectorId} for charging schedule {@chargingSchedule}.", activeChargingSchedule.CarId, activeChargingSchedule.OccpChargingConnectorId, activeChargingSchedule);
                continue;
            }
            alreadyControlledLoadPoints.Add((activeChargingSchedule.CarId, activeChargingSchedule.OccpChargingConnectorId));
            var result = await ForceSetLoadPointPower(correspondingLoadPoint, activeChargingSchedule.ChargingPower, maxAdditionalCurrent,
                    cancellationToken).ConfigureAwait(false);
            powerToControl -= result.powerIncrease;
            maxAdditionalCurrent -= result.currentIncrease;
        }

        if (powerToControl < 1)
        {
            loadPoints = loadPoints.OrderByDescending(l => l.Priority).ToList();
        }

        foreach (var loadPoint in loadPoints)
        {
            if (!alreadyControlledLoadPoints.Add((loadPoint.Car?.Id, loadPoint.OcppConnectorId)))
            {
                //Continue if this load point has already been controlled in the previous loop
                continue;
            }
            var result = await SetLoadPointPower(loadPoint, powerToControl, maxAdditionalCurrent, cancellationToken).ConfigureAwait(false);
            powerToControl -= result.powerIncrease;
            maxAdditionalCurrent -= result.currentIncrease;
        }
    }

    private async Task CalculateGeofences()
    {
        _logger.LogTrace("{method}()", nameof(CalculateGeofences));
        foreach (var car in _settings.CarsToManage)
        {
            if (car.Longitude == null || car.Latitude == null)
            {
                _logger.LogDebug("No location data for car {carId}. Do not calculate geofence", car.Id);
                car.DistanceToHomeGeofence = null;
                continue;
            }

            var homeDetectionVia = await _context.Cars
                .Where(c => c.Id == car.Id)
                .Select(c => c.HomeDetectionVia)
                .FirstAsync();

            if (homeDetectionVia != HomeDetectionVia.GpsLocation)
            {
                _logger.LogDebug("Car {carId} uses fleet telemetry but does not include tracking relevant fields. Do not calculate geofence", car.Id);
                car.DistanceToHomeGeofence = null;
                continue;
            }

            var distance = GetDistance(car.Longitude.Value, car.Latitude.Value,
                _configurationWrapper.HomeGeofenceLongitude(), _configurationWrapper.HomeGeofenceLatitude());
            _logger.LogDebug("Calculated distance to home geofence for car {carId}: {calculatedDistance}", car.Id, distance);
            var radius = _configurationWrapper.HomeGeofenceRadius();
            car.IsHomeGeofence = distance < radius;
            car.DistanceToHomeGeofence = (int)distance - radius;
        }
    }

    private double GetDistance(double longitude, double latitude, double otherLongitude, double otherLatitude)
    {
        var d1 = latitude * (Math.PI / 180.0);
        var num1 = longitude * (Math.PI / 180.0);
        var d2 = otherLatitude * (Math.PI / 180.0);
        var num2 = otherLongitude * (Math.PI / 180.0) - num1;
        var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) + Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);

        return 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
    }

    private async Task UpdateShouldStartStopChargingTimes(int powerToChargeWith)
    {
        _logger.LogTrace("{method}({powerToChargeWith})", nameof(UpdateShouldStartStopChargingTimes), powerToChargeWith);
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        foreach (var ocppConnectorState in _settings.OcppConnectorStates)
        {
            var ocppDatabaseData = await _context.OcppChargingStationConnectors
                .Where(c => c.Id == ocppConnectorState.Key)
                .Select(c => new
                {
                    c.MinCurrent,
                    c.MaxCurrent,
                    c.ConnectedPhasesCount,
                    c.AutoSwitchBetween1And3PhasesEnabled,
                    c.SwitchOffAtCurrent,
                    c.SwitchOnAtCurrent,
                })
                .FirstAsync().ConfigureAwait(false);
            var chargingPower = ocppConnectorState.Value.ChargingPower.Value;
            if (ocppDatabaseData.ConnectedPhasesCount == default)
            {
                _logger.LogError("Connected phases unknown for connector {connectorId}", ocppConnectorState.Key);
                return;
            }
            if (ocppDatabaseData.MinCurrent == default)
            {
                _logger.LogError("Min current unknown for connector {connectorId}", ocppConnectorState.Key);
                return;
            }
            if (ocppDatabaseData.MaxCurrent == default)
            {
                _logger.LogError("Max current unknown for connector {connectorId}", ocppConnectorState.Key);
                return;
            }
            if (ocppDatabaseData.ConnectedPhasesCount == default)
            {
                _logger.LogError("Connected phases unknown for connector {connectorId}", ocppConnectorState.Key);
                return;
            }
            if (ocppDatabaseData.SwitchOffAtCurrent == default)
            {
                _logger.LogError("Switch off current unknown for connector {connectorId}", ocppConnectorState.Key);
                return;
            }
            if (ocppDatabaseData.SwitchOnAtCurrent == default)
            {
                _logger.LogError("Switch on current unknown for connector {connectorId}", ocppConnectorState.Key);
                return;
            }
            var targetPower = powerToChargeWith;
            var minPhases = ocppDatabaseData.AutoSwitchBetween1And3PhasesEnabled ? 1 : ocppDatabaseData.ConnectedPhasesCount.Value;
            var shouldStartChargingPower =
                GetPowerAtPhasesAndCurrent(minPhases, ocppDatabaseData.SwitchOnAtCurrent.Value);
            ocppConnectorState.Value.ShouldStartCharging.Update(currentDate, shouldStartChargingPower < targetPower);
            var shouldStopChargingPower =
                GetPowerAtPhasesAndCurrent(minPhases, ocppDatabaseData.SwitchOffAtCurrent.Value);
            ocppConnectorState.Value.ShouldStopCharging.Update(currentDate, shouldStopChargingPower > targetPower);
            if (ocppDatabaseData.AutoSwitchBetween1And3PhasesEnabled)
            {
                var minPowerThreePhase =
                    GetPowerAtPhasesAndCurrent(ocppDatabaseData.ConnectedPhasesCount.Value, ocppDatabaseData.MinCurrent.Value);
                ocppConnectorState.Value.CanHandlePowerOnThreePhase.Update(currentDate, minPowerThreePhase < targetPower);
                var minPowerOnePhase =
                    GetPowerAtPhasesAndCurrent(ocppDatabaseData.ConnectedPhasesCount.Value, ocppDatabaseData.MinCurrent.Value);
                var maxPowerOnOnePhase =
                    GetPowerAtPhasesAndCurrent(ocppDatabaseData.ConnectedPhasesCount.Value, ocppDatabaseData.MaxCurrent.Value);
                ocppConnectorState.Value.CanHandlePowerOnOnePhase.Update(currentDate, (minPowerOnePhase < targetPower)
                                                                                      && (maxPowerOnOnePhase > targetPower));
            }
        }
        foreach (var car in _settings.CarsToManage)
        {
            var targetPower = powerToChargeWith;
            var phases = car.ActualPhases;
            var carSettings = await _context.Cars
                .Where(c => c.Id == car.Id)
                .Select(c => new
                {
                    c.MinimumAmpere,
                    c.SwitchOffAtCurrent,
                    c.SwitchOnAtCurrent,
                })
                .FirstAsync().ConfigureAwait(false);
            var switchOnPower = GetPowerAtPhasesAndCurrent(phases, carSettings.SwitchOnAtCurrent ?? carSettings.MinimumAmpere);
            var switchOffPower = GetPowerAtPhasesAndCurrent(phases, carSettings.SwitchOffAtCurrent ?? carSettings.MinimumAmpere);
            car.ShouldStartCharging.Update(currentDate, switchOnPower < targetPower);
            car.ShouldStopCharging.Update(currentDate, switchOffPower > targetPower);
        }
    }

    private int GetPowerAtPhasesAndCurrent(int connectedPhasesCount, decimal maxCurrent)
    {
        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        return (int)(connectedPhasesCount * maxCurrent * voltage);
    }

    private async Task<(int powerIncrease, decimal currentIncrease)> ForceSetLoadPointPower(DtoLoadpoint loadpoint, int powerToSet,
        decimal maxAdditionalCurrent, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({loadPoint.CarId}, {loadPoint.ConnectorId}, {powerToSet}, {maxAdditionalCurrent})",
            nameof(ForceSetLoadPointPower), loadpoint.Car?.Id, loadpoint.OcppConnectorId, powerToSet, maxAdditionalCurrent);
        var (minCurrent, maxCurrent, minPhases, maxPhases, useCarToManageChargingSpeed, canChangePhases) = await GetMinMaxCurrentsAndPhases(loadpoint, cancellationToken).ConfigureAwait(false);

        if (minPhases == default)
        {
            _logger.LogError("Min phases unknown for loadpoint {carId}, {connectorId}", loadpoint.Car?.Id, loadpoint.OcppConnectorId);
            return (0, 0);
        }

        if (maxPhases == default)
        {
            _logger.LogError("Max phases unknown for loadpoint {carId}, {connectorId}", loadpoint.Car?.Id, loadpoint.OcppConnectorId);
            return (0, 0);
        }

        if (minCurrent == default)
        {
            _logger.LogError("Min current unknown for loadpoint {carId}, {connectorId}", loadpoint.Car?.Id, loadpoint.OcppConnectorId);
            return (0, 0);
        }

        if (maxCurrent == default)
        {
            _logger.LogError("Max current unknown for loadpoint {carId}, {connectorId}", loadpoint.Car?.Id, loadpoint.OcppConnectorId);
            return (0, 0);
        }

        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        var currentToSetOnMaxPhases = (decimal)powerToSet / voltage / maxPhases.Value;
        var phasesToUse = maxPhases.Value;
        if (currentToSetOnMaxPhases < minCurrent)
        {
            phasesToUse = minPhases.Value;
        }
        var currentToSet = (decimal)powerToSet / voltage / phasesToUse;
        if (currentToSet < minCurrent)
        {
            currentToSet = minCurrent.Value;
        }
        else if (currentToSet > maxCurrent)
        {
            currentToSet = maxCurrent.Value;
        }
        var powerBeforeChanges = loadpoint.ActualChargingPower ?? 0;
        var currentBeforeChanges = loadpoint.ActualCurrent ?? 0;
        if (currentToSet > (currentBeforeChanges + maxAdditionalCurrent))
        {
            currentToSet = currentBeforeChanges + maxAdditionalCurrent;
        }

        bool isCharging;
        if (useCarToManageChargingSpeed)
        {
            isCharging = (loadpoint.Car!.State == CarStateEnum.Charging) && (loadpoint.Car!.IsHomeGeofence == true);
        }
        else
        {
            isCharging = loadpoint.OcppConnectorState?.IsCharging.Value ?? false;
        }

        if (isCharging)
        {
            if (useCarToManageChargingSpeed)
            {
                await _teslaService.SetAmp(loadpoint.Car!.Id, (int)currentToSet).ConfigureAwait(false);
                var actuallySetPower = GetPowerAtPhasesAndCurrent(loadpoint.Car.ActualPhases, currentToSet);
                return (actuallySetPower - powerBeforeChanges, currentToSet - currentBeforeChanges);
            }
            else
            {
                if (phasesToUse != loadpoint.ActualPhases!.Value)
                {
                    var chargeStopResult = await _ocppChargePointActionService.StopCharging(loadpoint.OcppConnectorId!.Value, cancellationToken).ConfigureAwait(false);
                    if (chargeStopResult.HasError)
                    {
                        _logger.LogError("Error stopping OCPP charge point for connector {loadpointId}: {errorMessage}",
                            loadpoint.OcppConnectorId, chargeStopResult.ErrorMessage);
                        return (0, 0);
                    }
                    _logger.LogTrace("Stopped OCPP charge point for connector {loadpointId} to change phases from {oldPhases} to {newPhases}",
                        loadpoint.OcppConnectorId, loadpoint.ActualPhases, phasesToUse);
                    return (-powerBeforeChanges, -currentBeforeChanges);
                }
                var ampChangeResult = await _ocppChargePointActionService.SetChargingCurrent(loadpoint.OcppConnectorId!.Value, currentToSet, phasesToUse, cancellationToken).ConfigureAwait(false);
                if (ampChangeResult.HasError)
                {
                    _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                        loadpoint.OcppConnectorId, ampChangeResult.ErrorMessage);
                    return (0, 0);
                }
                var actuallySetPower = GetPowerAtPhasesAndCurrent(phasesToUse, currentToSet);
                return (actuallySetPower, currentToSet);
            }
        }
        else
        {
            if (useCarToManageChargingSpeed)
            {
                await _teslaService.StartCharging(loadpoint.Car!.Id, (int)currentToSet).ConfigureAwait(false);
                var actuallySetPower = GetPowerAtPhasesAndCurrent(loadpoint.Car.ActualPhases, currentToSet);
                return (actuallySetPower - powerBeforeChanges, currentToSet - currentBeforeChanges);
            }
            else
            {
                var ampChangeResult = await _ocppChargePointActionService.StartCharging(loadpoint.OcppConnectorId!.Value, currentToSet, canChangePhases ? phasesToUse : null, cancellationToken).ConfigureAwait(false);
                if (ampChangeResult.HasError)
                {
                    _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                        loadpoint.OcppConnectorId, ampChangeResult.ErrorMessage);
                    return (0, 0);
                }
                var actuallySetPower = GetPowerAtPhasesAndCurrent(phasesToUse, currentToSet);
                return (actuallySetPower, currentToSet);
            }
        }

    }


    private async Task<(int powerIncrease, decimal currentIncrease)> SetLoadPointPower(DtoLoadpoint loadpoint, int powerToSet, decimal maxAdditionalCurrent, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({loadPoint.CarId}, {loadPoint.ConnectorId}, {powerToSet}, {maxAdditionalCurrent})",
            nameof(SetLoadPointPower), loadpoint.Car?.Id, loadpoint.OcppConnectorId, powerToSet, maxAdditionalCurrent);

        var (minCurrent, maxCurrent, minPhases, maxPhases, useCarToManageChargingSpeed, canChangePhases) = await GetMinMaxCurrentsAndPhases(loadpoint, cancellationToken).ConfigureAwait(false);

        // Decision: minPhases known?
        _logger.LogTrace("{method} decision: minPhases = {minPhases}", nameof(SetLoadPointPower), minPhases);
        if (minPhases == default)
        {
            _logger.LogError("Min phases unknown for loadpoint {carId}, {connectorId}", loadpoint.Car?.Id, loadpoint.OcppConnectorId);
            return (0, 0);
        }

        // Decision: maxPhases known?
        _logger.LogTrace("{method} decision: maxPhases = {maxPhases}", nameof(SetLoadPointPower), maxPhases);
        if (maxPhases == default)
        {
            _logger.LogError("Max phases unknown for loadpoint {carId}, {connectorId}", loadpoint.Car?.Id, loadpoint.OcppConnectorId);
            return (0, 0);
        }

        // Decision: minCurrent known?
        _logger.LogTrace("{method} decision: minCurrent = {minCurrent}", nameof(SetLoadPointPower), minCurrent);
        if (minCurrent == default)
        {
            _logger.LogError("Min current unknown for loadpoint {carId}, {connectorId}", loadpoint.Car?.Id, loadpoint.OcppConnectorId);
            return (0, 0);
        }

        // Decision: maxCurrent known?
        _logger.LogTrace("{method} decision: maxCurrent = {maxCurrent}", nameof(SetLoadPointPower), maxCurrent);
        if (maxCurrent == default)
        {
            _logger.LogError("Max current unknown for loadpoint {carId}, {connectorId}", loadpoint.Car?.Id, loadpoint.OcppConnectorId);
            return (0, 0);
        }

        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        var powerBeforeChanges = loadpoint.ActualChargingPower ?? 0;
        var currentBeforeChanges = loadpoint.ActualCurrent ?? 0;

        // Decision: useCarToManageChargingSpeed AND connector assigned?
        _logger.LogTrace("{method} decision: useCarToManageChargingSpeed = {useCarToManageChargingSpeed}, OcppConnectorId = {connectorId}", nameof(SetLoadPointPower), useCarToManageChargingSpeed, loadpoint.OcppConnectorId);
        if (useCarToManageChargingSpeed && loadpoint.OcppConnectorId != default)
        {
            #region Set OCPP to max power on OCPP loadpoints where car is directly controlled by TSC

            var anyOpenTransaction = await _context.OcppTransactions
                .Where(t => t.ChargingStationConnectorId == loadpoint.OcppConnectorId
                                        && t.EndDate == default)
                .AnyAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Decision: anyOpenTransaction?
            _logger.LogTrace("{method} decision: anyOpenTransaction = {anyOpenTransaction}", nameof(SetLoadPointPower), anyOpenTransaction);
            if (!anyOpenTransaction)
            {
                var chargeStartResponse = await _ocppChargePointActionService
                    .StartCharging(loadpoint.OcppConnectorId.Value, maxCurrent.Value, maxPhases, cancellationToken)
                    .ConfigureAwait(false);

                // Decision: chargeStartResponse.HasError?
                _logger.LogTrace("{method} decision: chargeStartResponse.HasError = {hasError}", nameof(SetLoadPointPower), chargeStartResponse.HasError);
                if (chargeStartResponse.HasError)
                {
                    _logger.LogError("Error start OCPP charge point with max power for connector {loadpointId}: {errorMessage}",
                        loadpoint.OcppConnectorId, chargeStartResponse.ErrorMessage);
                    return (0, 0);
                }
            }
            else
            {
                if (_settings.OcppConnectorStates.TryGetValue(loadpoint.OcppConnectorId.Value, out var connectorState))
                {
                    // Decision: connectorState.LastSetCurrent vs maxCurrent and connectorState.LastSetPhases vs maxPhases
                    _logger.LogTrace("{method} decision: LastSetCurrent = {lastSetCurrent}, maxCurrent = {maxCurrent}, LastSetPhases = {lastSetPhases}, maxPhases = {maxPhases}",
                        nameof(SetLoadPointPower),
                        connectorState.LastSetCurrent.Value,
                        maxCurrent,
                        connectorState.LastSetPhases.Value,
                        maxPhases);

                    if ((connectorState.LastSetCurrent.Value != maxCurrent) || (connectorState.LastSetPhases.Value != maxPhases))
                    {
                        var chargeUpdateResponse = await _ocppChargePointActionService
                            .SetChargingCurrent(loadpoint.OcppConnectorId.Value, maxCurrent.Value, maxPhases, cancellationToken)
                            .ConfigureAwait(false);

                        // Decision: chargeUpdateResponse.HasError?
                        _logger.LogTrace("{method} decision: chargeUpdateResponse.HasError = {hasError}", nameof(SetLoadPointPower), chargeUpdateResponse.HasError);
                        if (chargeUpdateResponse.HasError)
                        {
                            _logger.LogError("Error setting OCPP charge point to max power for connector {loadpointId}: {errorMessage}",
                                loadpoint.OcppConnectorId, chargeUpdateResponse.ErrorMessage);
                            return (0, 0);
                        }
                    }
                }
            }

            #endregion
        }

        bool isCharging;
        // Decision: useCarToManageChargingSpeed branch for isCharging
        _logger.LogTrace("{method} decision: useCarToManageChargingSpeed = {useCarToManageChargingSpeed}, CarState = {carState}, IsHomeGeofence = {isHomeGeofence}, OcppConnectorState IsCharging = {ocppIsCharging}",
            nameof(SetLoadPointPower),
            useCarToManageChargingSpeed,
            loadpoint.Car?.State,
            loadpoint.Car?.IsHomeGeofence,
            loadpoint.OcppConnectorState?.IsCharging.Value);

        if (useCarToManageChargingSpeed)
        {
            isCharging = (loadpoint.Car!.State == CarStateEnum.Charging) && (loadpoint.Car!.IsHomeGeofence == true);
        }
        else
        {
            isCharging = loadpoint.OcppConnectorState?.IsCharging.Value ?? false;
        }

        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();

        if (isCharging)
        {
            #region Stop charging if required

            // Decision: useCarToManageChargingSpeed branch for stopping
            _logger.LogTrace("{method} decision: isCharging = true, useCarToManageChargingSpeed = {useCarToManageChargingSpeed}", nameof(SetLoadPointPower), useCarToManageChargingSpeed);
            if (useCarToManageChargingSpeed)
            {
                // Decision: ShouldStopCharging and LastChanged threshold
                _logger.LogTrace("{method} decision: ShouldStopCharging = {shouldStop}, LastChanged = {lastChanged}, threshold = {threshold}",
                    nameof(SetLoadPointPower),
                    loadpoint.Car!.ShouldStopCharging.Value,
                    loadpoint.Car.ShouldStopCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOff());

                if ((loadpoint.Car!.ShouldStopCharging.Value == true)
                    && (loadpoint.Car.ShouldStopCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOff())))
                {
                    // ToDo: add error handling
                    await _teslaService.StopCharging(loadpoint.Car.Id).ConfigureAwait(false);
                    return (-powerBeforeChanges, -currentBeforeChanges);
                }
            }
            else
            {
                // Decision: ShouldStopCharging and LastChanged threshold for OCPP
                _logger.LogTrace("{method} decision: ShouldStopCharging = {shouldStop}, LastChanged = {lastChanged}, threshold = {threshold}",
                    nameof(SetLoadPointPower),
                    loadpoint.OcppConnectorState!.ShouldStopCharging.Value,
                    loadpoint.OcppConnectorState.ShouldStopCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOff());

                if ((loadpoint.OcppConnectorState!.ShouldStopCharging.Value == true)
                    && (loadpoint.OcppConnectorState.ShouldStopCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOff())))
                {
                    var result = await _ocppChargePointActionService.StopCharging(loadpoint.OcppConnectorId!.Value, cancellationToken);
                    // Decision: result.HasError?
                    _logger.LogTrace("{method} decision: StopCharging result.HasError = {hasError}", nameof(SetLoadPointPower), result.HasError);
                    if (result.HasError)
                    {
                        _logger.LogError("Error stopping OCPP charge point for connector {loadpointId}: {errorMessage}",
                            loadpoint.OcppConnectorId, result.ErrorMessage);
                        return (0, 0);
                    }

                    return (-powerBeforeChanges, -currentBeforeChanges);
                }
            }

            #endregion
        }

        if (!isCharging)
        {
            #region Start charging if required

            // Decision: isCharging = false, useCarToManageChargingSpeed
            _logger.LogTrace("{method} decision: isCharging = false, useCarToManageChargingSpeed = {useCar}", nameof(SetLoadPointPower), useCarToManageChargingSpeed);

            if (useCarToManageChargingSpeed)
            {
                // Decision: ShouldStartCharging and LastChanged threshold
                _logger.LogTrace("{method} decision: ShouldStartCharging = {shouldStart}, LastChanged = {lastChanged}, threshold = {threshold}, powerToSet = {powerToSet}, voltage = {voltage}, actualPhases = {phases}, maxAdditionalCurrent = {maxAdditionalCurrent}",
                    nameof(SetLoadPointPower),
                    loadpoint.Car!.ShouldStartCharging.Value,
                    loadpoint.Car.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn(),
                    powerToSet,
                    voltage,
                    loadpoint.Car.ActualPhases,
                    maxAdditionalCurrent);

                if ((loadpoint.Car!.ShouldStartCharging.Value == true)
                    && (loadpoint.Car.ShouldStartCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
                {
                    var currentToStartChargingWith = powerToSet / voltage / loadpoint.Car.ActualPhases;
                    if (maxAdditionalCurrent < (currentToStartChargingWith - currentBeforeChanges))
                    {
                        currentToStartChargingWith = (int)(currentBeforeChanges + maxAdditionalCurrent);
                    }
                    if (currentToStartChargingWith < minCurrent)
                    {
                        return (0, 0);
                    }

                    if (currentToStartChargingWith > maxCurrent)
                    {
                        currentToStartChargingWith = maxCurrent.Value;
                    }

                    if (loadpoint.Car.SocLimit < (loadpoint.Car.SoC + _constants.MinimumSocDifference))
                    {
                        _logger.LogTrace("Car {carId} has a SOC limit of {socLimit}, which is too low to start charging. Current SOC: {currentSoc}",
                            loadpoint.Car.Id, loadpoint.Car.SocLimit, loadpoint.Car.SoC);
                        return (0, 0);
                    }
                    await _teslaService.StartCharging(loadpoint.Car.Id, currentToStartChargingWith).ConfigureAwait(false);
                    var actuallySetPower = GetPowerAtPhasesAndCurrent(loadpoint.Car.ActualPhases, currentToStartChargingWith);
                    return (actuallySetPower, currentToStartChargingWith);
                }
            }
            else
            {
                // Decision: ShouldStartCharging and LastChanged threshold for OCPP
                _logger.LogTrace("{method} decision: ShouldStartCharging = {shouldStart}, LastChanged = {lastChanged}, threshold = {threshold}",
                    nameof(SetLoadPointPower),
                    loadpoint.OcppConnectorState!.ShouldStartCharging.Value,
                    loadpoint.OcppConnectorState.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn());

                if ((loadpoint.OcppConnectorState!.ShouldStartCharging.Value == true)
                    && (loadpoint.OcppConnectorState.ShouldStartCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
                {
                    int phasesToStartChargingWith;
                    // Decision: minPhases == maxPhases?
                    _logger.LogTrace("{method} decision: minPhases = {minPhases}, maxPhases = {maxPhases}", nameof(SetLoadPointPower), minPhases, maxPhases);
                    if (minPhases.Value == maxPhases.Value)
                    {
                        phasesToStartChargingWith = minPhases.Value;
                    }
                    else
                    {
                        // Decision: connector can handle power on one phase and not three
                        _logger.LogTrace("{method} decision: CanHandlePowerOnOnePhase = {onePhase}, CanHandlePowerOnThreePhase = {threePhase}",
                            nameof(SetLoadPointPower),
                            loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.Value,
                            loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.Value);

                        if ((loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.Value == true)
                            && (loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.Value != true))
                        {
                            phasesToStartChargingWith = minPhases.Value;
                        }
                        else if ((loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.Value == true)
                                 && (loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.Value != true))
                        {
                            phasesToStartChargingWith = maxPhases.Value;
                        }
                        else if ((loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.Value == true)
                                 && (loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.LastChanged < loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.LastChanged))
                        {
                            phasesToStartChargingWith = maxPhases.Value;
                        }
                        else
                        {
                            phasesToStartChargingWith = minPhases.Value;
                        }

                        // Log chosen phases
                        _logger.LogTrace("{method} decision: chosen phasesToStartChargingWith = {phases}", nameof(SetLoadPointPower), phasesToStartChargingWith);
                    }

                    var currentToStartChargingWith = powerToSet / voltage / phasesToStartChargingWith;
                    if (maxAdditionalCurrent < (currentToStartChargingWith - currentBeforeChanges))
                    {
                        currentToStartChargingWith = (int)(currentBeforeChanges + maxAdditionalCurrent);
                    }
                    if (currentToStartChargingWith < minCurrent)
                    {
                        return (0, 0);
                    }
                    if (currentToStartChargingWith > maxCurrent)
                    {
                        currentToStartChargingWith = maxCurrent.Value;
                    }

                    if ((loadpoint.OcppConnectorState.LastSetCurrent.Value > 0)
                        && (loadpoint.OcppConnectorState.IsCarFullyCharged.Value == true))
                    {
                        _logger.LogTrace("Do not try to start charging as last set Current is greater than 0 and car is fully charged");
                        return (0, 0);
                    }

                    var result = await _ocppChargePointActionService.StartCharging(loadpoint.OcppConnectorId!.Value, currentToStartChargingWith, canChangePhases ? phasesToStartChargingWith : null, cancellationToken).ConfigureAwait(false);
                    // Decision: result.HasError?
                    _logger.LogTrace("{method} decision: StartCharging result.HasError = {hasError}", nameof(SetLoadPointPower), result.HasError);
                    if (result.HasError)
                    {
                        _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                            loadpoint.OcppConnectorId, result.ErrorMessage);
                        return (0, 0);
                    }

                    var actuallySetPower = GetPowerAtPhasesAndCurrent(phasesToStartChargingWith, currentToStartChargingWith);
                    return (actuallySetPower, currentToStartChargingWith);
                }
            }

            #endregion
        }

        if (!isCharging)
        {
            #region Let charging stopped if should not start

            // Decision: isCharging = false, useCarToManageChargingSpeed
            _logger.LogTrace("{method} decision: isCharging = false, useCarToManageChargingSpeed = {useCar}", nameof(SetLoadPointPower), useCarToManageChargingSpeed);

            if (useCarToManageChargingSpeed)
            {
                // Decision: ShouldStartCharging and LastChanged threshold
                _logger.LogTrace("{method} decision: ShouldStartCharging = {shouldStart}, LastChanged = {lastChanged}, threshold = {threshold}",
                    nameof(SetLoadPointPower),
                    loadpoint.Car!.ShouldStartCharging.Value,
                    loadpoint.Car.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn());

                if ((loadpoint.Car!.ShouldStartCharging.Value == false)
                    || (loadpoint.Car.ShouldStartCharging.LastChanged > (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
                {
                    return (0, 0);
                }
            }
            else
            {
                // Decision: ShouldStartCharging and LastChanged threshold for OCPP
                _logger.LogTrace("{method} decision: ShouldStartCharging = {shouldStart}, LastChanged = {lastChanged}, threshold = {threshold}",
                    nameof(SetLoadPointPower),
                    loadpoint.OcppConnectorState!.ShouldStartCharging.Value,
                    loadpoint.OcppConnectorState.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn());

                if ((loadpoint.OcppConnectorState!.ShouldStartCharging.Value == false)
                    || (loadpoint.OcppConnectorState.ShouldStartCharging.LastChanged > (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
                {
                    return (0, 0);
                }
            }

            #endregion
        }

        if (isCharging)
        {
            #region Continue to charge with new values

            _logger.LogTrace("{method} decision: isCharging = true, useCarToManageChargingSpeed = {useCar}", nameof(SetLoadPointPower), useCarToManageChargingSpeed);
            if (useCarToManageChargingSpeed)
            {
                var currentToChargeWith = powerToSet / voltage / loadpoint.Car!.ActualPhases;
                if (maxAdditionalCurrent < (currentToChargeWith - currentBeforeChanges))
                {
                    currentToChargeWith = (int)(currentBeforeChanges + maxAdditionalCurrent);
                }
                if (currentToChargeWith < minCurrent)
                {
                    currentToChargeWith = minCurrent.Value;
                }
                if (currentToChargeWith > maxCurrent)
                {
                    currentToChargeWith = maxCurrent.Value;
                }

                await _teslaService.SetAmp(loadpoint.Car.Id, currentToChargeWith).ConfigureAwait(false);
                var actuallySetPower = GetPowerAtPhasesAndCurrent(loadpoint.Car.ActualPhases, currentToChargeWith);
                return (actuallySetPower - powerBeforeChanges, currentToChargeWith - currentBeforeChanges);
            }
            else
            {
                var phasesToChargeWith = loadpoint.OcppConnectorState!.PhaseCount.Value;

                // Decision: minPhases == maxPhases?
                _logger.LogTrace("{method} decision: minPhases = {minPhases}, maxPhases = {maxPhases}", nameof(SetLoadPointPower), minPhases, maxPhases);
                if (minPhases.Value != maxPhases.Value)
                {
                    // Decision: inability to handle one or three phase
                    _logger.LogTrace("{method} decision: CanHandleOnePhase = {onePhase}, LastChangedOnePhase = {changedOne}, CanHandleThreePhase = {threePhase}, LastChangedThreePhase = {changedThree}, threshold = {threshold}",
                        nameof(SetLoadPointPower),
                        loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.Value,
                        loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.LastChanged,
                        loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.Value,
                        loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.LastChanged,
                        currentDate - _configurationWrapper.TimespanUntilSwitchOn());

                    if ((loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.Value == false)
                        && (loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOn()))
                        && (loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.Value == true))
                    {
                        var result = await _ocppChargePointActionService.StopCharging(loadpoint.OcppConnectorId!.Value, cancellationToken);
                        // Decision: result.HasError?
                        _logger.LogTrace("{method} decision: StopCharging result.HasError = {hasError}", nameof(SetLoadPointPower), result.HasError);
                        if (result.HasError)
                        {
                            _logger.LogError("Error stopping OCPP charge point for connector {loadpointId}: {errorMessage}",
                                loadpoint.OcppConnectorId, result.ErrorMessage);
                            return (0, 0);
                        }

                        return (-powerBeforeChanges, -currentBeforeChanges);
                    }
                    if ((loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.Value == false)
                        && (loadpoint.OcppConnectorState.CanHandlePowerOnThreePhase.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOff()))
                        && (loadpoint.OcppConnectorState.CanHandlePowerOnOnePhase.Value == true))
                    {
                        var result = await _ocppChargePointActionService.StopCharging(loadpoint.OcppConnectorId!.Value, cancellationToken);
                        // Decision: result.HasError?
                        _logger.LogTrace("{method} decision: StopCharging result.HasError = {hasError}", nameof(SetLoadPointPower), result.HasError);
                        if (result.HasError)
                        {
                            _logger.LogError("Error stopping OCPP charge point for connector {loadpointId}: {errorMessage}",
                                loadpoint.OcppConnectorId, result.ErrorMessage);
                            return (0, 0);
                        }

                        return (-powerBeforeChanges, -currentBeforeChanges);
                    }
                }

                if (phasesToChargeWith == default)
                {
                    if (loadpoint.OcppConnectorState!.LastSetPhases.Value == default)
                    {
                        phasesToChargeWith = maxPhases.Value;
                    }
                    else
                    {
                        phasesToChargeWith = loadpoint.OcppConnectorState!.LastSetPhases.Value.Value;
                    }
                }

                var currentToChargeWith = (decimal)powerToSet / voltage / phasesToChargeWith.Value;
                if (maxAdditionalCurrent < (currentToChargeWith - currentBeforeChanges))
                {
                    currentToChargeWith = currentBeforeChanges + maxAdditionalCurrent;
                }
                if (currentToChargeWith < minCurrent)
                {
                    currentToChargeWith = minCurrent.Value;
                }
                if (currentToChargeWith > maxCurrent)
                {
                    currentToChargeWith = maxCurrent.Value;
                }
                var ampChangeResult = await _ocppChargePointActionService.SetChargingCurrent(loadpoint.OcppConnectorId!.Value, currentToChargeWith, canChangePhases ? phasesToChargeWith : null, cancellationToken).ConfigureAwait(false);
                // Decision: ampChangeResult.HasError?
                _logger.LogTrace("{method} decision: ampChangeResult.HasError = {hasError}", nameof(SetLoadPointPower), ampChangeResult.HasError);
                if (ampChangeResult.HasError)
                {
                    _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                        loadpoint.OcppConnectorId, ampChangeResult.ErrorMessage);
                    return (0, 0);
                }

                var actuallySetPower = GetPowerAtPhasesAndCurrent(phasesToChargeWith.Value, currentToChargeWith);
                return (powerBeforeChanges - actuallySetPower, currentBeforeChanges - currentToChargeWith);
            }

            #endregion
        }

        _logger.LogError("No path meets all conditions, check data why not: {@loadpoint}", loadpoint);
        return (0, 0);
    }


    private async Task<(int? minCurrent, int? maxCurrent, int? minPhases, int? maxPhases, bool useCarToManageChargingSpeed, bool canChangePhases)> GetMinMaxCurrentsAndPhases(DtoLoadpoint loadpoint, CancellationToken cancellationToken)
    {
        int? minCurrent = null;
        int? maxCurrent = null;
        int? minPhases = null;
        int? maxPhases = null;
        //ToDo: Set this to false if car is no Tesla as soon as other car brands are supported
        var useCarToManageChargingSpeed = loadpoint.Car != default;
        bool canChangePhases = false;
        if (loadpoint.Car != default)
        {
            var carConfigValues = await _context.Cars
                .Where(c => c.Id == loadpoint.Car.Id)
                .Select(c => new { c.MinimumAmpere, c.MaximumAmpere, })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            minCurrent = carConfigValues.MinimumAmpere;
            maxCurrent = carConfigValues?.MaximumAmpere;
            minPhases = loadpoint.Car!.ActualPhases;
            maxPhases = loadpoint.Car!.ActualPhases;
        }

        if (loadpoint.OcppConnectorId != default)
        {
            var chargingConnectorConfigValues = await _context.OcppChargingStationConnectors
                .Where(c => c.Id == loadpoint.OcppConnectorId)
                .Select(c => new
                {
                    c.MinCurrent,
                    c.MaxCurrent,
                    c.ConnectedPhasesCount,
                    c.AutoSwitchBetween1And3PhasesEnabled,
                })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (minCurrent == default)
            {
                minCurrent = chargingConnectorConfigValues.MinCurrent;
            }
            else if ((!useCarToManageChargingSpeed) && (chargingConnectorConfigValues.MinCurrent > minCurrent))
            {
                minCurrent = chargingConnectorConfigValues.MinCurrent;
            }

            if (maxCurrent == default || chargingConnectorConfigValues.MaxCurrent < maxCurrent)
            {
                maxCurrent = chargingConnectorConfigValues.MaxCurrent;
            }

            if (minPhases == default)
            {
                minPhases = chargingConnectorConfigValues.AutoSwitchBetween1And3PhasesEnabled
                    ? 1
                    : chargingConnectorConfigValues.ConnectedPhasesCount;
            }

            if (maxPhases == default)
            {
                maxPhases = chargingConnectorConfigValues.ConnectedPhasesCount;
            }
            canChangePhases = chargingConnectorConfigValues.AutoSwitchBetween1And3PhasesEnabled;
        }

        return (minCurrent, maxCurrent, minPhases, maxPhases, useCarToManageChargingSpeed, canChangePhases);
    }

    private async Task<int> CalculatePowerToControl(int currentChargingPower, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(CalculatePowerToControl));
        var resultConfigurations = await _context.ModbusResultConfigurations.Select(r => r.UsedFor).ToListAsync(cancellationToken: cancellationToken);
        resultConfigurations.AddRange(await _context.RestValueResultConfigurations.Select(r => r.UsedFor).ToListAsync(cancellationToken: cancellationToken));
        resultConfigurations.AddRange(await _context.MqttResultConfigurations.Select(r => r.UsedFor).ToListAsync(cancellationToken: cancellationToken));
        var availablePowerSources = new DtoAvailablePowerSources()
        {
            InverterPowerAvailable = resultConfigurations.Any(c => c == ValueUsage.InverterPower),
            GridPowerAvailable = resultConfigurations.Any(c => c == ValueUsage.GridPower),
            HomeBatteryPowerAvailable = resultConfigurations.Any(c => c == ValueUsage.HomeBatteryPower),
        };

        var buffer = _configurationWrapper.PowerBuffer();
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);
        var averagedOverage = _settings.Overage ?? _constants.DefaultOverage;
        _logger.LogDebug("Averaged overage {averagedOverage}", averagedOverage);

        if (!availablePowerSources.GridPowerAvailable
            && availablePowerSources.InverterPowerAvailable)
        {
            _logger.LogDebug("Using Inverter power {inverterPower} minus current combined charging power {chargingPowerAtHome} as overage",
                _settings.InverterPower, currentChargingPower);
            if (_settings.InverterPower == default)
            {
                _logger.LogWarning("Inverter power is not available, can not calculate power to control.");
                return 0;
            }
            averagedOverage = _settings.InverterPower.Value - currentChargingPower;
        }
        var overage = averagedOverage - buffer;
        _logger.LogDebug("Calculated overage {overage} after subtracting power buffer ({buffer})", overage, buffer);

        overage = await AddHomeBatterStateToPowerCalculation(overage, cancellationToken).ConfigureAwait(false);
        return overage + currentChargingPower;
    }

    private async Task<int> AddHomeBatterStateToPowerCalculation(int overage, CancellationToken cancellationToken)
    {
        var dynamicHomeBatteryMinSocEnabled = _configurationWrapper.DynamicHomeBatteryMinSoc();
        var homeBatteryMinSoc = _configurationWrapper.HomeBatteryMinSoc();
        if (dynamicHomeBatteryMinSocEnabled)
        {
            var dynamicHomeBatteryMinSoc = await CalculateDynamicHomeBatteryMinSoc(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Dynamic Home Battery Min SoC is enabled, using dynamic value {dynamicHomeBatteryMinSoc} instead of configured value {homeBatteryMinSoc}.", dynamicHomeBatteryMinSoc, homeBatteryMinSoc);
            homeBatteryMinSoc = dynamicHomeBatteryMinSoc;
        }
        _logger.LogDebug("Home battery min soc: {homeBatteryMinSoc}", homeBatteryMinSoc);
        var homeBatteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
        _logger.LogDebug("Home battery should charging power: {homeBatteryMaxChargingPower}", homeBatteryMaxChargingPower);
        if (homeBatteryMinSoc == default || homeBatteryMaxChargingPower == default)
        {
            return overage;
        }
        var batteryMinChargingPower = GetBatteryTargetChargingPower(homeBatteryMinSoc);
        var actualHomeBatterySoc = _settings.HomeBatterySoc;
        _logger.LogDebug("Home battery actual soc: {actualHomeBatterySoc}", actualHomeBatterySoc);
        var actualHomeBatteryPower = _settings.HomeBatteryPower;
        _logger.LogDebug("Home battery actual power: {actualHomeBatteryPower}", actualHomeBatteryPower);
        if (actualHomeBatteryPower == default)
        {
            return overage;
        }
        var overageToIncrease = actualHomeBatteryPower.Value - batteryMinChargingPower;
        overage += overageToIncrease;
        var inverterAcOverload = (_configurationWrapper.MaxInverterAcPower() - _settings.InverterPower) * (-1);
        if (inverterAcOverload > 0)
        {
            _logger.LogDebug("As inverter power is higher than max inverter AC power, overage is reduced by overload");
            overage -= (inverterAcOverload.Value - batteryMinChargingPower);
        }
        return overage;
    }

    private int GetBatteryTargetChargingPower(int? minSoc)
    {
        var actualHomeBatterySoc = _settings.HomeBatterySoc;
        var homeBatteryMinSoc = minSoc;
        var homeBatteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
        if (actualHomeBatterySoc < homeBatteryMinSoc)
        {
            return homeBatteryMaxChargingPower ?? 0;
        }

        return 0;
    }

    private async Task<int?> CalculateDynamicHomeBatteryMinSoc(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(CalculateDynamicHomeBatteryMinSoc));
        var homeBatteryUsableEnergy = _configurationWrapper.HomeBatteryUsableEnergy();
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        if (homeBatteryUsableEnergy == default)
        {
            _logger.LogWarning("Dynamic Home Battery Min SoC is enabled, but no usable energy configured. Using configured home battery min soc.");
            return null;
        }
        var currentHomeBatterySoc = _settings.HomeBatterySoc;
        if (currentHomeBatterySoc == default)
        {
            _logger.LogWarning("Dynamic Home Battery Min SoC is enabled, bur current Soc is unknown.");
            return null;
        }

        var homeGeofenceLatitude = _configurationWrapper.HomeGeofenceLatitude();
        var homeGeofenceLongitude = _configurationWrapper.HomeGeofenceLongitude();
        var nextSunset = _sunCalculator.CalculateSunset(homeGeofenceLatitude,
            homeGeofenceLongitude, currentDate);
        if (nextSunset < currentDate)
        {
            nextSunset = _sunCalculator.CalculateSunset(homeGeofenceLatitude,
                homeGeofenceLongitude, currentDate.AddDays(1));
        }
        if (nextSunset == default)
        {
            _logger.LogWarning("Could not calculate sunrise for current date {currentDate}. Using configured home battery min soc.", currentDate);
            return null;
        }
        //Do not try to fully charge to allow some buffer with fast reaction time compared to cars.
        var fullBatterySoc = 95;
        var requiredEnergyForFullBattery = (int)(homeBatteryUsableEnergy.Value * ((fullBatterySoc - currentHomeBatterySoc.Value) / 100.0m));
        if (requiredEnergyForFullBattery < 1)
        {
            _logger.LogDebug("No energy required to charge home battery to full.");
            return null;
        }
        var predictionInterval = TimeSpan.FromHours(1);
        var fullBatteryTargetTime = new DateTimeOffset(nextSunset.Value.Year, nextSunset.Value.Month, nextSunset.Value.Day,
            nextSunset.Value.Hour, 0, 0, nextSunset.Value.Offset);
        var currentDateWith0Minutes = new DateTimeOffset(currentDate.Year, currentDate.Month, currentDate.Day, currentDate.Hour, 0, 0, currentDate.Offset);
        var predictedSurplusPerSlices = await _energyDataService.GetPredictedSurplusPerSlice(currentDateWith0Minutes, fullBatteryTargetTime, predictionInterval, cancellationToken).ConfigureAwait(false);
        return _homeBatteryEnergyCalculator.CalculateRequiredInitialStateOfChargeFraction(
            predictedSurplusPerSlices, homeBatteryUsableEnergy.Value, 5, fullBatterySoc);

    }

    private async Task<DtoTimeZonedChargingTarget?> GetNextTarget(int carId, CancellationToken cancellationToken)
    {
        var chargingTargets = await _context.CarChargingTargets
            .Where(c => c.CarId == carId)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (chargingTargets.Count < 1)
        {
            _logger.LogDebug("No charging targets found for car {carId}.", carId);
            return null;
        }
        DtoTimeZonedChargingTarget? nextTarget = null;
        foreach (var carChargingTarget in chargingTargets)
        {
            var nextTargetUtc = GetNextTargetUtc(carChargingTarget);
            if (nextTarget == default || (nextTargetUtc < nextTarget.NextExecutionTime))
            {
                nextTarget = new()
                {
                    Id = carChargingTarget.Id,
                    TargetSoc = carChargingTarget.TargetSoc,
                    TargetDate = carChargingTarget.TargetDate,
                    TargetTime = carChargingTarget.TargetTime,
                    RepeatOnMondays = carChargingTarget.RepeatOnMondays,
                    RepeatOnTuesdays = carChargingTarget.RepeatOnTuesdays,
                    RepeatOnWednesdays = carChargingTarget.RepeatOnWednesdays,
                    RepeatOnThursdays = carChargingTarget.RepeatOnThursdays,
                    RepeatOnFridays = carChargingTarget.RepeatOnFridays,
                    RepeatOnSaturdays = carChargingTarget.RepeatOnSaturdays,
                    RepeatOnSundays = carChargingTarget.RepeatOnSundays,
                    ClientTimeZone = carChargingTarget.ClientTimeZone,
                    CarId = carChargingTarget.CarId,
                    NextExecutionTime = nextTargetUtc,
                };
            }
        }
        return nextTarget;
    }

    /// <summary>
    /// When targetTimeUtc is null it will generate the eraliest possible charging schedule, otherwise the latest possible charging schedule.
    /// </summary>
    /// <param name="chargingTargetSoc"></param>
    /// <param name="targetTimeUtc"></param>
    /// <param name="carUsableEnergy"></param>
    /// <param name="carSoC"></param>
    /// <param name="maxPhases"></param>
    /// <param name="maxCurrent"></param>
    /// <param name="carId"></param>
    /// <param name="chargingConnectorId"></param>
    /// <returns></returns>
    private DtoChargingSchedule? GenerateEarliestOrLatestPossibleChargingSchedule(int chargingTargetSoc,
        DateTimeOffset? targetTimeUtc,
        int? carUsableEnergy, int? carSoC, int? maxPhases, int? maxCurrent, int? carId, int? chargingConnectorId)
    {
        _logger.LogTrace(
            "{method}({chargingTargetSoc}, {targetTimeUtc}, {usableEnergy}, {soc}, {maxPhases}, {maxCurrent}, {carId}, {chargingConnectorId})",
            nameof(GenerateEarliestOrLatestPossibleChargingSchedule),
            chargingTargetSoc, targetTimeUtc, carUsableEnergy, carSoC, maxPhases, maxCurrent, carId, chargingConnectorId);

        var energyToCharge = CalculateEnergyToCharge(
            chargingTargetSoc,
            carSoC,
            carUsableEnergy);

        if (energyToCharge == default || energyToCharge < 1)
        {
            return null;
        }

        var maxChargingPower = GetMaxChargingPower(maxPhases, maxCurrent);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (maxChargingPower == default || maxChargingPower <= 0)
        {
            _logger.LogWarning("No valid charging power found for car with usable energy {usableEnergy} and SoC {soc}.", carUsableEnergy, carSoC);
            return null;
        }

        var chargingDuration = CalculateChargingDuration(
            energyToCharge.Value,
            maxChargingPower.Value);

        if (targetTimeUtc == default)
        {
            return new DtoChargingSchedule(carId, chargingConnectorId)
            {
                StartTime = _dateTimeProvider.DateTimeOffSetUtcNow(),
                EndTime = _dateTimeProvider.DateTimeOffSetUtcNow() + chargingDuration,
                ChargingPower = (int)maxChargingPower,
            };
        }

        var startTime = targetTimeUtc.Value - chargingDuration;

        return new DtoChargingSchedule(carId, chargingConnectorId)
        {
            StartTime = startTime,
            EndTime = targetTimeUtc.Value,
            ChargingPower = (int)maxChargingPower,
        };
    }

    private double? GetMaxChargingPower(int? maxPhases, int? maxCurrent)
    {
        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        if (maxPhases == default || maxCurrent == default)
        {
            return null;
        }
        var maxChargingPower = CalculateMaxChargingPower(
            maxCurrent.Value,
            maxPhases.Value,
            voltage);
        return maxChargingPower;
    }

    private async Task<(int? UsableEnergy, int? carSoC, int? maxPhases, int? maxCurrent, int? minPhases, int? minCurrent)> GetChargingScheduleRelevantData(int? carId, int? chargingConnectorId)
    {
        var connectorData = chargingConnectorId != default
            ? await _context.OcppChargingStationConnectors
                .Where(c => c.Id == chargingConnectorId)
                .Select(c => new
                {
                    c.ConnectedPhasesCount,
                    c.MaxCurrent,
                    c.MinCurrent,
                })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false)
            : null;

        var carData = carId != default
            ? await _context.Cars
                .Where(c => c.Id == carId)
                .Select(c => new
                {
                    c.MaximumAmpere,
                    c.UsableEnergy,
                    c.MinimumAmpere,
                })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false)
            : null;

        var carSetting = _settings.Cars.FirstOrDefault(c => c.Id == carId);
        var carSoC = carSetting?.SoC;
        var carPhases = carSetting?.ActualPhases;

        var maxPhases = CalculateMaxValue(connectorData?.ConnectedPhasesCount, carPhases);
        var maxCurrent = CalculateMaxValue(connectorData?.MaxCurrent, carData?.MaximumAmpere);
        var minPhases = CalculateMinValue(connectorData?.ConnectedPhasesCount, carPhases);
        var minCurrent = CalculateMinValue(connectorData?.MinCurrent, carData?.MinimumAmpere);


        return (carData?.UsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent);
    }

    // Helpers — pure, primitive parameters

    private int? CalculateMaxValue(
        int? connectorValue,
        int? carValue)
    {
        if (connectorValue == default)
        {
            return carValue;
        }

        if (carValue != default && carValue < connectorValue)
        {
            return carValue;
        }

        return connectorValue;
    }

    private int? CalculateMinValue(
        int? connectorValue,
        int? carValue)
    {
        if (connectorValue == default)
        {
            return carValue;
        }

        if (carValue != default && carValue > connectorValue)
        {
            return carValue;
        }

        return connectorValue;
    }

    private int? CalculateEnergyToCharge(
        int chargingTargetSoc,
        int? currentSoC,
        int? usableEnergy)
    {
        if (usableEnergy == default || currentSoC == default || usableEnergy <= 0)
        {
            return default;
        }

        var socDiff = chargingTargetSoc - currentSoC;
        var energyWh = socDiff * usableEnergy * 10; // soc*10 vs usableEnergy*1000 scale

        return energyWh > 0
            ? energyWh
            : default;
    }

    private double CalculateMaxChargingPower(
        int maxCurrent,
        int maxPhases,
        int voltage)
    {
        // W = A * phases * V
        return (double)maxCurrent * maxPhases * voltage;
    }

    private TimeSpan CalculateChargingDuration(
        int energyToChargeWh,
        double maxChargingPowerW)
    {
        // hours = Wh / W
        return TimeSpan.FromHours(energyToChargeWh / maxChargingPowerW);
    }

    internal DateTimeOffset GetNextTargetUtc(CarChargingTarget chargingTarget)
    {
        var tz = string.IsNullOrWhiteSpace(chargingTarget.ClientTimeZone)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(chargingTarget.ClientTimeZone);

        var currentUtcDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var earliestExecutionTime = TimeZoneInfo.ConvertTime(currentUtcDate, tz);

        DateTimeOffset? candidate;

        if (chargingTarget.TargetDate.HasValue)
        {
            candidate = new DateTimeOffset(chargingTarget.TargetDate.Value, chargingTarget.TargetTime,
                tz.GetUtcOffset(new(chargingTarget.TargetDate.Value, chargingTarget.TargetTime)));

            //candidate is irrelevant if it is in the past
            if (candidate > earliestExecutionTime)
            {
                // if no repetition is set, return the candidate
                if (!chargingTarget.RepeatOnMondays
                    && !chargingTarget.RepeatOnTuesdays
                    && !chargingTarget.RepeatOnWednesdays
                    && !chargingTarget.RepeatOnThursdays
                    && !chargingTarget.RepeatOnFridays
                    && !chargingTarget.RepeatOnSaturdays
                    && !chargingTarget.RepeatOnSundays)
                {
                    return candidate.Value.ToUniversalTime();
                }
                // if repetition is set the set date is considered as the earliest execution time. But we still need to check if it is the first enabled weekday
                earliestExecutionTime = candidate.Value;
            }
            // otherwise fall back to the repeating schedule
        }


        //ToDO: take earliest execution time and use first repeating weekday at or after that date
        for (var i = 0; i < 7; i++)
        {
            var date = earliestExecutionTime.Date.AddDays(i);
            var isEnabled = date.DayOfWeek switch
            {
                DayOfWeek.Monday => chargingTarget.RepeatOnMondays,
                DayOfWeek.Tuesday => chargingTarget.RepeatOnTuesdays,
                DayOfWeek.Wednesday => chargingTarget.RepeatOnWednesdays,
                DayOfWeek.Thursday => chargingTarget.RepeatOnThursdays,
                DayOfWeek.Friday => chargingTarget.RepeatOnFridays,
                DayOfWeek.Saturday => chargingTarget.RepeatOnSaturdays,
                DayOfWeek.Sunday => chargingTarget.RepeatOnSundays,
                _ => false,
            };

            if (!isEnabled)
                continue;

            // build the local DateTimeOffset for that day + target time
            var localDt = date + chargingTarget.TargetTime.ToTimeSpan();
            candidate = new DateTimeOffset(localDt, tz.GetUtcOffset(localDt));

            if (candidate >= earliestExecutionTime)
            {
                return candidate.Value.ToUniversalTime();
            }
        }

        throw new InvalidOperationException(
            "Could not find any upcoming target. Please check TargetDate or repeat flags."
        );
    }

    private async Task SetChargingStationToMaxPowerIfTeslaIsConnected(
        DtoLoadpoint loadPoint, DateTime currentLocalDate, CancellationToken cancellationToken)
    {
        if (loadPoint.Car == default || loadPoint.OcppConnectorState == default || loadPoint.OcppConnectorId == default)
        {
            throw new ArgumentNullException(nameof(loadPoint), "Car, OcppChargingConnector and OCPP Charging Connector ID are note allowed to be null here");
        }

        if (loadPoint.Car.AutoFullSpeedCharge || (loadPoint.Car.ShouldStartChargingSince < currentLocalDate))
        {
            _logger.LogTrace("Loadpoint with car ID {carId} and chargingConnectorId {chargingConnectorId} should currently charge. Setting ocpp station to max current charge.", loadPoint.Car.Id, loadPoint.OcppConnectorId);
            if (loadPoint.OcppConnectorState.IsCarFullyCharged.Value != true)
            {
                _logger.LogInformation("Not fully charged Tesla connected to OCPP Charging station.");
                var chargePointInfo = await _context.OcppChargingStationConnectors
                    .Where(c => c.Id == loadPoint.OcppConnectorId)
                    .Select(c => new
                    {
                        c.MaxCurrent,
                        c.ConnectedPhasesCount,
                    })
                    .FirstAsync(cancellationToken: cancellationToken);
                if (chargePointInfo.MaxCurrent == default)
                {
                    _logger.LogError("Chargepoint not fully configured, can not set charging current");
                    return;
                }
                if (!loadPoint.OcppConnectorState.IsCharging.Value)
                {
                    await _ocppChargePointActionService.StartCharging(loadPoint.OcppConnectorId.Value,
                        chargePointInfo.MaxCurrent.Value,
                        chargePointInfo.ConnectedPhasesCount,
                        cancellationToken).ConfigureAwait(false);
                }
                else if ((loadPoint.Car.ChargerPilotCurrent < loadPoint.Car.MaximumAmpere)
                         && (loadPoint.Car.ChargerPilotCurrent < chargePointInfo.MaxCurrent))
                {

                    await _ocppChargePointActionService.SetChargingCurrent(loadPoint.OcppConnectorId.Value,
                        chargePointInfo.MaxCurrent.Value,
                        chargePointInfo.ConnectedPhasesCount,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
