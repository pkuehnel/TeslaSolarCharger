using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
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
    private readonly IConstants _constants;
    private readonly ITeslaService _teslaService;
    private readonly IShouldStartStopChargingCalculator _shouldStartStopChargingCalculator;
    private readonly IEnergyDataService _energyDataService;
    private readonly IValidFromToSplitter _validFromToSplitter;

    public ChargingServiceV2(ILogger<ChargingServiceV2> logger,
        IConfigurationWrapper configurationWrapper,
        ILoadPointManagementService loadPointManagementService,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        IOcppChargePointActionService ocppChargePointActionService,
        ISettings settings,
        ITscOnlyChargingCostService tscOnlyChargingCostService,
        IConstants constants,
        ITeslaService teslaService,
        IShouldStartStopChargingCalculator shouldStartStopChargingCalculator,
        IEnergyDataService energyDataService,
        IValidFromToSplitter validFromToSplitter)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _loadPointManagementService = loadPointManagementService;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _ocppChargePointActionService = ocppChargePointActionService;
        _settings = settings;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
        _constants = constants;
        _teslaService = teslaService;
        _shouldStartStopChargingCalculator = shouldStartStopChargingCalculator;
        _energyDataService = energyDataService;
        _validFromToSplitter = validFromToSplitter;
    }

    public async Task SetNewChargingValues(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(SetNewChargingValues));
        if (!_configurationWrapper.UseChargingServiceV2())
        {
            return;
        }
        await CalculateGeofences();
        var chargingLoadPoints = _loadPointManagementService.GetLoadPointsWithChargingDetails();
        var powerToControl = await CalculatePowerToControl(chargingLoadPoints.Select(l => l.ChargingPower).Sum(), cancellationToken).ConfigureAwait(false);
        await _shouldStartStopChargingCalculator.UpdateShouldStartStopChargingTimes(powerToControl);
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var loadPointsToManage = await _loadPointManagementService.GetLoadPointsToManage().ConfigureAwait(false);
        var chargingSchedules = await GenerateChargingSchedules(currentDate, loadPointsToManage, cancellationToken).ConfigureAwait(false);

        _settings.ChargingSchedules = new ConcurrentBag<DtoChargingSchedule>(chargingSchedules);

        _logger.LogDebug("Final calculated power to control: {powerToControl}", powerToControl);
        var alreadyControlledLoadPoints = new HashSet<(int? carId, int? connectorId)>();
        currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var activeChargingSchedules = chargingSchedules.Where(s => s.ValidFrom <= currentDate).ToList();

        var maxAdditionalCurrent = _configurationWrapper.MaxCombinedCurrent() - chargingLoadPoints.Select(l => l.ChargingCurrent).Sum();
        foreach (var activeChargingSchedule in activeChargingSchedules)
        {
            if (powerToControl < activeChargingSchedule.OnlyChargeOnAtLeastSolarPower)
            {
                _logger.LogDebug("Skipping charging schedule {@chargingSchedule} as is only placeholder and car should charge with solar power", activeChargingSchedule);
                continue;
            }

            if (activeChargingSchedule.CarId != default)
            {
                var car = _settings.Cars.First(c => c.Id == activeChargingSchedule.CarId.Value);
                if ((car.PluggedIn != true) || (car.IsHomeGeofence != true))
                {
                    _logger.LogWarning("Can not execute active charging schedule as car {carId} is not plugged in at home", activeChargingSchedule.CarId);
                    continue;
                }
            }

            if (activeChargingSchedule.OccpChargingConnectorId != default)
            {
                var connectorState = _settings.OcppConnectorStates.GetValueOrDefault(activeChargingSchedule.OccpChargingConnectorId.Value);
                if (connectorState == default || (!connectorState.IsPluggedIn.Value))
                {
                    _logger.LogWarning("Can not execute charging schedule as charging connector {chargingConnectorId} is not connected via OCPP or not plugged in", activeChargingSchedule.OccpChargingConnectorId);
                    continue;
                }
            }

            var correspondingLoadPoint = chargingLoadPoints.FirstOrDefault(l => l.CarId == activeChargingSchedule.CarId
                                                                                && l.ChargingConnectorId == activeChargingSchedule.OccpChargingConnectorId);
            if (correspondingLoadPoint == default)
            {
                correspondingLoadPoint = new DtoLoadPointWithCurrentChargingValues()
                {
                    CarId = activeChargingSchedule.CarId,
                    ChargingConnectorId = activeChargingSchedule.OccpChargingConnectorId,
                    ChargingPower = 0,
                    ChargingVoltage = _settings.AverageHomeGridVoltage ?? 230,
                    ChargingCurrent = 0,
                };
            }
            alreadyControlledLoadPoints.Add((activeChargingSchedule.CarId, activeChargingSchedule.OccpChargingConnectorId));
            var result = await ForceSetLoadPointPower(activeChargingSchedule.CarId, activeChargingSchedule.OccpChargingConnectorId, correspondingLoadPoint, activeChargingSchedule.ChargingPower, maxAdditionalCurrent,
                    cancellationToken).ConfigureAwait(false);
            powerToControl -= result.powerIncrease;
            maxAdditionalCurrent -= result.currentIncrease;
        }

        if (powerToControl < 1)
        {
            loadPointsToManage = loadPointsToManage.OrderByDescending(l => l.ChargingPriority).ToList();
        }

        foreach (var loadPoint in loadPointsToManage)
        {
            if (!alreadyControlledLoadPoints.Add((loadPoint.CarId, loadPoint.ChargingConnectorId)))
            {
                //Continue if this load point has already been controlled in the previous loop
                continue;
            }
            if (loadPoint.CarId != default)
            {
                var car = _settings.Cars.First(c => c.Id == loadPoint.CarId.Value);
                if ((car.PluggedIn != true) || (car.IsHomeGeofence != true))
                {
                    _logger.LogInformation("Can not execute active charging schedule as car {carId} is not plugged in at home", loadPoint.CarId);
                    continue;
                }
            }

            if (loadPoint.ChargingConnectorId != default)
            {
                var connectorState = _settings.OcppConnectorStates.GetValueOrDefault(loadPoint.ChargingConnectorId.Value);
                if (connectorState == default || (!connectorState.IsPluggedIn.Value))
                {
                    _logger.LogInformation("Can not execute charging schedule as charging connector {chargingConnectorId} is not connected via OCPP or not plugged in", loadPoint.ChargingConnectorId);
                    continue;
                }
            }
            var correspondingLoadPoint = chargingLoadPoints.FirstOrDefault(l => l.CarId == loadPoint.CarId
                                                                                && l.ChargingConnectorId == loadPoint.ChargingConnectorId) ??
                                         new DtoLoadPointWithCurrentChargingValues()
            {
                CarId = loadPoint.CarId,
                ChargingConnectorId = loadPoint.ChargingConnectorId,
                ChargingPower = 0,
                ChargingVoltage = _settings.AverageHomeGridVoltage ?? 230,
                ChargingCurrent = 0,
            };
            var result = await SetLoadPointPower(loadPoint.CarId, loadPoint.ChargingConnectorId, correspondingLoadPoint, powerToControl, maxAdditionalCurrent, cancellationToken).ConfigureAwait(false);
            powerToControl -= result.powerIncrease;
            maxAdditionalCurrent -= result.currentIncrease;
        }
    }

    private async Task<List<DtoChargingSchedule>> GenerateChargingSchedules(DateTimeOffset currentDate, List<DtoLoadPointOverview> loadPointsToManage,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({currentDate}, {loadPointsToManage})", nameof(GenerateChargingSchedules), currentDate, loadPointsToManage.Count);
        var chargingSchedules = new List<DtoChargingSchedule>();
        foreach (var loadpoint in loadPointsToManage)
        {
            if (loadpoint.CarId != default)
            {
                var car = _settings.Cars.First(c => c.Id == loadpoint.CarId.Value);
                if (car.ChargeModeV2 == ChargeModeV2.Manual || car.ChargeModeV2 == ChargeModeV2.Off)
                {
                    continue;
                }
                var (carUsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent) = await GetChargingScheduleRelevantData(loadpoint.CarId, loadpoint.ChargingConnectorId).ConfigureAwait(false);
                if (carUsableEnergy == default || carSoC == default || maxPhases == default || maxCurrent == default)
                {
                    _logger.LogWarning("Can not schedule charging as at least one required value is unknown.");
                    continue;
                }
                if (car.MinimumSoC > car.SoC || car.ChargeModeV2 == ChargeModeV2.MaxPower)
                {
                    var energyToCharge = car.ChargeModeV2 == ChargeModeV2.MaxPower
                    ? 100000
                    : CalculateEnergyToCharge(
                        car.MinimumSoC,
                        car.SoC ?? 0,
                        carUsableEnergy.Value);
                    var earliestPossibleChargingSchedule =
                        GenerateEarliestOrLatestPossibleChargingSchedule(null, currentDate,
                            energyToCharge, maxPhases.Value, maxCurrent.Value, car.Id, loadpoint.ChargingConnectorId);
                    if (earliestPossibleChargingSchedule != default)
                    {
                        chargingSchedules.Add(earliestPossibleChargingSchedule);
                        //Do not plan anything else, before min Soc is reached
                        continue;
                    }
                }

                var nextTarget = await GetNextTarget(car.Id, cancellationToken).ConfigureAwait(false);
                if (nextTarget != default)
                {
                    var energyToCharge = CalculateEnergyToCharge(
                        nextTarget.TargetSoc,
                        car.SoC ?? 0,
                        carUsableEnergy.Value);
                    var maxPower = GetPowerAtPhasesAndCurrent(maxPhases.Value, maxCurrent.Value);
                    if (_configurationWrapper.UsePredictedSolarPowerGenerationForChargingSchedules() && minPhases != default && minCurrent != default)
                    {
                        var currentFullHour = new DateTimeOffset(currentDate.Year, currentDate.Month, currentDate.Day, currentDate.Hour, 0, 0, currentDate.Offset);
                        var surplusTimeSpanInHours = 1;
                        var fullHourAfterNextTarget = new DateTimeOffset(nextTarget.NextExecutionTime.Year, nextTarget.NextExecutionTime.Month, nextTarget.NextExecutionTime.Day, nextTarget.NextExecutionTime.Hour + surplusTimeSpanInHours, 0, 0, nextTarget.NextExecutionTime.Offset);
                        var predictedSurplusSlices = await _energyDataService
                            .GetPredictedSurplusPerSlice(currentFullHour, fullHourAfterNextTarget, TimeSpan.FromHours(surplusTimeSpanInHours), cancellationToken)
                            .ConfigureAwait(false);
                        var minPower = GetPowerAtPhasesAndCurrent(minPhases.Value, minCurrent.Value);
                        var maxPowerCappedPredictedHoursWithAtLeastMinPowerSurpluses = predictedSurplusSlices
                            .Where(s => s.Value > minPower)
                            .OrderBy(s => s.Key)
                            .ToDictionary(s => s.Key, s => s.Value > maxPower ? maxPower : s.Value);
                        var scheduledSolarEnergyCharged = 0;
                        foreach (var maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus in maxPowerCappedPredictedHoursWithAtLeastMinPowerSurpluses)
                        {
                            var startDate = maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key < currentDate
                                ? currentDate
                                : maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key;
                            var endDate =
                                maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key.AddHours(surplusTimeSpanInHours) > nextTarget.NextExecutionTime
                                    ? nextTarget.NextExecutionTime
                                    : maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key.AddHours(surplusTimeSpanInHours);
                            var energyChargedInThisSchedule =
                                (int)(maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Value * (endDate - startDate).TotalHours);
                            scheduledSolarEnergyCharged += energyChargedInThisSchedule;
                            var chargingScheduleForThisHour = new DtoChargingSchedule(loadpoint.CarId.Value, loadpoint.ChargingConnectorId)
                            {
                                ValidFrom = startDate,
                                ValidTo = endDate,
                                ChargingPower = maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Value,
                                OnlyChargeOnAtLeastSolarPower = minPower,
                            };
                            chargingSchedules.Add(chargingScheduleForThisHour);
                            var remainingenergyToCharge = energyToCharge - scheduledSolarEnergyCharged;
                            if (remainingenergyToCharge <= 0)
                            {
                                var tooMuchChargedEnergy = -remainingenergyToCharge;
                                var hoursToReduce = (double)tooMuchChargedEnergy / chargingScheduleForThisHour.ChargingPower;
                                chargingScheduleForThisHour.ValidTo = chargingScheduleForThisHour.ValidTo.AddHours(-hoursToReduce);
                                _logger.LogDebug("Scheduled enough solar energy to reach target soc, so do not plan any further charging schedules");
                                break;
                            }
                        }
                    }

                    var remainingEnergyToCoverFromGrid = energyToCharge -
                                                         (int)chargingSchedules.Select(s => (s.ValidTo - s.ValidFrom).TotalHours * s.ChargingPower).Sum();
                    var electricityPrices = await _tscOnlyChargingCostService.GetPricesInTimeSpan(currentDate, nextTarget.NextExecutionTime);
                    var endTimeOrderedElectricityPrices = electricityPrices.OrderBy(p => p.ValidTo).ToList();
                    var lastGridPrice = endTimeOrderedElectricityPrices.LastOrDefault();
                    if ((lastGridPrice == default) || (lastGridPrice.ValidTo < nextTarget.NextExecutionTime))
                    {
                        //Do not plan for target if last grid price is earlier than next execution time
                        continue;
                    }

                    var (splittedGridPrices, splittedChargingSchedules) =
                        _validFromToSplitter.SplitByBoundaries(electricityPrices, chargingSchedules, currentDate, nextTarget.NextExecutionTime);
                    var gridPriceOrderedElectricityPrices = splittedGridPrices
                        .OrderBy(p => p.GridPrice)
                        .ThenByDescending(p => p.ValidFrom)
                        .ToList();
                    foreach (var gridPriceOrderedElectricityPrice in gridPriceOrderedElectricityPrices)
                    {
                        if (remainingEnergyToCoverFromGrid <= 0)
                        {
                            break;
                        }

                        var correspondingChargingSchedule = splittedChargingSchedules
                            .FirstOrDefault(cs => cs.ValidFrom == gridPriceOrderedElectricityPrice.ValidFrom
                                                  && cs.ValidTo == gridPriceOrderedElectricityPrice.ValidTo);
                        if (correspondingChargingSchedule == default)
                        {
                            correspondingChargingSchedule = new DtoChargingSchedule(loadpoint.CarId.Value, loadpoint.ChargingConnectorId)
                            {
                                ValidFrom = gridPriceOrderedElectricityPrice.ValidFrom,
                                ValidTo = gridPriceOrderedElectricityPrice.ValidTo,
                            };
                            chargingSchedules.Add(correspondingChargingSchedule);
                        }
                        
                        var maxPowerIncrease = maxPower - correspondingChargingSchedule.ChargingPower;
                        correspondingChargingSchedule.ChargingPower += maxPowerIncrease;
                        correspondingChargingSchedule.OnlyChargeOnAtLeastSolarPower = null;
                        remainingEnergyToCoverFromGrid -= (int)(maxPowerIncrease * (gridPriceOrderedElectricityPrice.ValidTo - gridPriceOrderedElectricityPrice.ValidFrom).TotalHours);
                        if (remainingEnergyToCoverFromGrid < 0)
                        {
                            var hoursToReduce = (double)-remainingEnergyToCoverFromGrid / correspondingChargingSchedule.ChargingPower;
                            correspondingChargingSchedule.ValidFrom = correspondingChargingSchedule.ValidFrom.AddHours(hoursToReduce);
                        }
                    }
                }
            }
        }

        return chargingSchedules;
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

    private int GetPowerAtPhasesAndCurrent(int connectedPhasesCount, decimal maxCurrent)
    {
        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        return (int)(connectedPhasesCount * maxCurrent * voltage);
    }

    private async Task<(int powerIncrease, decimal currentIncrease)> ForceSetLoadPointPower(int? carId, int? chargingConnectorId,
        DtoLoadPointWithCurrentChargingValues loadpoint, int powerToSet,
        decimal maxAdditionalCurrent, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({loadPoint.CarId}, {loadPoint.ConnectorId}, {powerToSet}, {maxAdditionalCurrent})",
            nameof(ForceSetLoadPointPower), carId, chargingConnectorId, powerToSet, maxAdditionalCurrent);
        var (minCurrent, maxCurrent, minPhases, maxPhases, useCarToManageChargingSpeed, canChangePhases, _, _, _) = await GetMinMaxCurrentsAndPhases(carId, chargingConnectorId, cancellationToken).ConfigureAwait(false);
        if (minPhases == default)
        {
            _logger.LogError("Min phases unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }
        if (maxPhases == default)
        {
            _logger.LogError("Max phases unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }
        if (minCurrent == default)
        {
            _logger.LogError("Min current unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }
        if (maxCurrent == default)
        {
            _logger.LogError("Max current unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
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
        var powerBeforeChanges = loadpoint.ChargingPower;
        var currentBeforeChanges = loadpoint.ChargingCurrent;
        if (currentToSet > (currentBeforeChanges + maxAdditionalCurrent))
        {
            currentToSet = currentBeforeChanges + maxAdditionalCurrent;
        }

        bool isCharging;
        if (useCarToManageChargingSpeed)
        {
            var car = _settings.Cars.First(c => c.Id == carId);
            isCharging = (car.State == CarStateEnum.Charging) && (car.IsHomeGeofence == true);
        }
        else
        {
            //ChargingConnectorId can not be null as if ueCarToManageChargingSpeed is false, the loadpoint must have an OCPP connector assigned
            var ocppConnectorState = _settings.OcppConnectorStates.GetValueOrDefault(chargingConnectorId!.Value);
            isCharging = ocppConnectorState?.IsCharging.Value ?? false;
        }

        if (isCharging)
        {
            if (useCarToManageChargingSpeed)
            {
                await _teslaService.SetAmp(carId!.Value, (int)currentToSet).ConfigureAwait(false);
                //charging phases can not be null as useCarToManageChargingSpeed is true and charging
                var actuallySetPower = GetPowerAtPhasesAndCurrent(loadpoint.ChargingPhases!.Value, currentToSet);
                return (actuallySetPower - powerBeforeChanges, currentToSet - currentBeforeChanges);
            }
            else
            {
                //charging phases can not be null as charging
                if (phasesToUse != loadpoint.ChargingPhases!.Value)
                {
                    var chargeStopResult = await _ocppChargePointActionService.StopCharging(chargingConnectorId!.Value, cancellationToken).ConfigureAwait(false);
                    if (chargeStopResult.HasError)
                    {
                        _logger.LogError("Error stopping OCPP charge point for connector {loadpointId}: {errorMessage}",
                            chargingConnectorId!.Value, chargeStopResult.ErrorMessage);
                        return (0, 0);
                    }
                    _logger.LogTrace("Stopped OCPP charge point for connector {loadpointId} to change phases from {oldPhases} to {newPhases}",
                        chargingConnectorId!.Value, loadpoint.ChargingPhases, phasesToUse);
                    return (-powerBeforeChanges, -currentBeforeChanges);
                }
                var ampChangeResult = await _ocppChargePointActionService.SetChargingCurrent(chargingConnectorId!.Value, currentToSet,  canChangePhases ? phasesToUse : null, cancellationToken).ConfigureAwait(false);
                if (ampChangeResult.HasError)
                {
                    _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                        chargingConnectorId!.Value, ampChangeResult.ErrorMessage);
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
                await _teslaService.StartCharging(loadpoint.CarId!.Value, (int)currentToSet).ConfigureAwait(false);
                var actuallySetPower = GetPowerAtPhasesAndCurrent(loadpoint.ChargingPhases!.Value, currentToSet);
                return (actuallySetPower - powerBeforeChanges, currentToSet - currentBeforeChanges);
            }
            else
            {
                var ampChangeResult = await _ocppChargePointActionService.StartCharging(loadpoint.ChargingConnectorId!.Value, currentToSet, canChangePhases ? phasesToUse : null, cancellationToken).ConfigureAwait(false);
                if (ampChangeResult.HasError)
                {
                    _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                        loadpoint.ChargingConnectorId, ampChangeResult.ErrorMessage);
                    return (0, 0);
                }
                var actuallySetPower = GetPowerAtPhasesAndCurrent(phasesToUse, currentToSet);
                return (actuallySetPower, currentToSet);
            }
        }

    }


    private async Task<(int powerIncrease, decimal currentIncrease)> SetLoadPointPower(int? carId, int? chargingConnectorId,
        DtoLoadPointWithCurrentChargingValues correspondingLoadPoint, int powerToSet, decimal maxAdditionalCurrent,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({loadPoint.CarId}, {loadPoint.ConnectorId}, {powerToSet}, {maxAdditionalCurrent})",
            nameof(SetLoadPointPower), carId, chargingConnectorId, powerToSet, maxAdditionalCurrent);

        var (minCurrent, maxCurrent, minPhases, maxPhases, useCarToManageChargingSpeed, canChangePhases, carChargeMode, connectorChargeMode, maxSoc) = await GetMinMaxCurrentsAndPhases(carId, chargingConnectorId, cancellationToken).ConfigureAwait(false);

        // Decision: minPhases known?
        _logger.LogTrace("{method} decision: minPhases = {minPhases}", nameof(SetLoadPointPower), minPhases);
        if (minPhases == default)
        {
            _logger.LogError("Min phases unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }

        // Decision: maxPhases known?
        _logger.LogTrace("{method} decision: maxPhases = {maxPhases}", nameof(SetLoadPointPower), maxPhases);
        if (maxPhases == default)
        {
            _logger.LogError("Max phases unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }

        // Decision: minCurrent known?
        _logger.LogTrace("{method} decision: minCurrent = {minCurrent}", nameof(SetLoadPointPower), minCurrent);
        if (minCurrent == default)
        {
            _logger.LogError("Min current unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }

        // Decision: maxCurrent known?
        _logger.LogTrace("{method} decision: maxCurrent = {maxCurrent}", nameof(SetLoadPointPower), maxCurrent);
        if (maxCurrent == default)
        {
            _logger.LogError("Max current unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }

        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        var powerBeforeChanges = correspondingLoadPoint.ChargingPower;
        var currentBeforeChanges = correspondingLoadPoint.ChargingCurrent;

        // Decision: useCarToManageChargingSpeed AND connector assigned?
        _logger.LogTrace("{method} decision: useCarToManageChargingSpeed = {useCarToManageChargingSpeed}, OcppConnectorId = {connectorId}", nameof(SetLoadPointPower), useCarToManageChargingSpeed, chargingConnectorId);
        if (useCarToManageChargingSpeed && chargingConnectorId != default)
        {
            #region Set OCPP to max power on OCPP loadpoints where car is directly controlled by TSC

            var anyOpenTransaction = await _context.OcppTransactions
                .Where(t => t.ChargingStationConnectorId == chargingConnectorId
                                        && t.EndDate == default)
                .AnyAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Decision: anyOpenTransaction?
            _logger.LogTrace("{method} decision: anyOpenTransaction = {anyOpenTransaction}", nameof(SetLoadPointPower), anyOpenTransaction);
            if (!anyOpenTransaction)
            {
                var chargeStartResponse = await _ocppChargePointActionService
                    .StartCharging(chargingConnectorId.Value, maxCurrent.Value, maxPhases, cancellationToken)
                    .ConfigureAwait(false);

                // Decision: chargeStartResponse.HasError?
                _logger.LogTrace("{method} decision: chargeStartResponse.HasError = {hasError}", nameof(SetLoadPointPower), chargeStartResponse.HasError);
                if (chargeStartResponse.HasError)
                {
                    _logger.LogError("Error start OCPP charge point with max power for connector {loadpointId}: {errorMessage}",
                        chargingConnectorId, chargeStartResponse.ErrorMessage);
                    return (0, 0);
                }
            }
            else
            {
                if (_settings.OcppConnectorStates.TryGetValue(chargingConnectorId.Value, out var connectorState))
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
                            .SetChargingCurrent(chargingConnectorId.Value, maxCurrent.Value, maxPhases, cancellationToken)
                            .ConfigureAwait(false);

                        // Decision: chargeUpdateResponse.HasError?
                        _logger.LogTrace("{method} decision: chargeUpdateResponse.HasError = {hasError}", nameof(SetLoadPointPower), chargeUpdateResponse.HasError);
                        if (chargeUpdateResponse.HasError)
                        {
                            _logger.LogError("Error setting OCPP charge point to max power for connector {loadpointId}: {errorMessage}",
                                chargingConnectorId, chargeUpdateResponse.ErrorMessage);
                            return (0, 0);
                        }
                    }
                }
            }

            #endregion
        }
        var car = _settings.Cars.FirstOrDefault(c => c.Id == carId);
        DtoOcppConnectorState? ocppConnectorState = null;
        if (chargingConnectorId != default && _settings.OcppConnectorStates.TryGetValue(chargingConnectorId.Value, out var state))
        {
            ocppConnectorState = state;
        }
        bool isCharging;
        // Decision: useCarToManageChargingSpeed branch for isCharging
        _logger.LogTrace("{method} decision: useCarToManageChargingSpeed = {useCarToManageChargingSpeed}, CarState = {carState}, IsHomeGeofence = {isHomeGeofence}, OcppConnectorState IsCharging = {ocppIsCharging}",
            nameof(SetLoadPointPower),
            useCarToManageChargingSpeed,
            car?.State,
            car?.IsHomeGeofence,
            ocppConnectorState?.IsCharging.Value);

        if (useCarToManageChargingSpeed)
        {
            isCharging = (car!.State == CarStateEnum.Charging) && (car!.IsHomeGeofence == true);
        }
        else
        {
            isCharging = ocppConnectorState?.IsCharging.Value ?? false;
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
                    car!.ShouldStopCharging.Value,
                    car.ShouldStopCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOff());

                if ((carChargeMode == ChargeModeV2.Off)
                    || ((carChargeMode == ChargeModeV2.Auto)
                        &&(car!.ShouldStopCharging.Value == true)
                        && (car.ShouldStopCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOff())))
                    || ((carChargeMode == ChargeModeV2.Auto)
                        && (maxSoc <= car.SoC)))
                {
                    // ToDo: add error handling
                    await _teslaService.StopCharging(car.Id).ConfigureAwait(false);
                    return (-powerBeforeChanges, -currentBeforeChanges);
                }
            }
            else
            {
                // Decision: ShouldStopCharging and LastChanged threshold for OCPP
                _logger.LogTrace("{method} decision: ShouldStopCharging = {shouldStop}, LastChanged = {lastChanged}, threshold = {threshold}",
                    nameof(SetLoadPointPower),
                    ocppConnectorState!.ShouldStopCharging.Value,
                    ocppConnectorState.ShouldStopCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOff());

                var wrongPhaseCount = ((ocppConnectorState.PhaseCount.Value == 1)
                    && (ocppConnectorState.CanHandlePowerOnOnePhase.Value == false)
                    && (ocppConnectorState.CanHandlePowerOnThreePhase.Value == true)
                    && (ocppConnectorState.CanHandlePowerOnOnePhase.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOn()))
                    && (ocppConnectorState.CanHandlePowerOnThreePhase.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
                    || (ocppConnectorState.PhaseCount.Value == 3
                    && (ocppConnectorState.CanHandlePowerOnOnePhase.Value == true)
                    && (ocppConnectorState.CanHandlePowerOnThreePhase.Value == false)
                    && (ocppConnectorState.CanHandlePowerOnOnePhase.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOff()))
                    && (ocppConnectorState.CanHandlePowerOnThreePhase.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOff())));

                if ((connectorChargeMode == ChargeModeV2.Off)
                    || ((connectorChargeMode == ChargeModeV2.Auto)
                        && (ocppConnectorState!.ShouldStopCharging.Value == true)
                        && (ocppConnectorState.ShouldStopCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOff())))
                    || wrongPhaseCount)
                {
                    var result = await _ocppChargePointActionService.StopCharging(chargingConnectorId!.Value, cancellationToken);
                    // Decision: result.HasError?
                    _logger.LogTrace("{method} decision: StopCharging result.HasError = {hasError}", nameof(SetLoadPointPower), result.HasError);
                    if (result.HasError)
                    {
                        _logger.LogError("Error stopping OCPP charge point for connector {loadpointId}: {errorMessage}",
                            chargingConnectorId, result.ErrorMessage);
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
                    car!.ShouldStartCharging.Value,
                    car.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn(),
                    powerToSet,
                    voltage,
                    car.ActualPhases,
                    maxAdditionalCurrent);

                if ((carChargeMode == ChargeModeV2.Auto)
                    && (car!.ShouldStartCharging.Value == true)
                    && (car.ShouldStartCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
                {
                    var currentToStartChargingWith = powerToSet / voltage / car.ActualPhases;
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

                    if (car.SocLimit < (car.SoC + _constants.MinimumSocDifference))
                    {
                        _logger.LogTrace("Car {carId} has a SOC limit of {socLimit}, which is too low to start charging. Current SOC: {currentSoc}",
                            car.Id, car.SocLimit, car.SoC);
                        return (0, 0);
                    }
                    await _teslaService.StartCharging(car.Id, currentToStartChargingWith).ConfigureAwait(false);
                    var actuallySetPower = GetPowerAtPhasesAndCurrent(car.ActualPhases, currentToStartChargingWith);
                    return (actuallySetPower, currentToStartChargingWith);
                }
            }
            else
            {
                // Decision: ShouldStartCharging and LastChanged threshold for OCPP
                _logger.LogTrace("{method} decision: ShouldStartCharging = {shouldStart}, LastChanged = {lastChanged}, threshold = {threshold}",
                    nameof(SetLoadPointPower),
                    ocppConnectorState!.ShouldStartCharging.Value,
                    ocppConnectorState.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn());

                if ((connectorChargeMode == ChargeModeV2.Auto)
                    && (ocppConnectorState!.ShouldStartCharging.Value == true)
                    && (ocppConnectorState.ShouldStartCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
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
                            ocppConnectorState.CanHandlePowerOnOnePhase.Value,
                            ocppConnectorState.CanHandlePowerOnThreePhase.Value);

                        if ((ocppConnectorState.CanHandlePowerOnOnePhase.Value == true)
                            && (ocppConnectorState.CanHandlePowerOnThreePhase.Value != true))
                        {
                            phasesToStartChargingWith = minPhases.Value;
                        }
                        else if ((ocppConnectorState.CanHandlePowerOnThreePhase.Value == true)
                                 && (ocppConnectorState.CanHandlePowerOnOnePhase.Value != true))
                        {
                            phasesToStartChargingWith = maxPhases.Value;
                        }
                        else if ((ocppConnectorState.CanHandlePowerOnThreePhase.Value == true)
                                 && ocppConnectorState.CanHandlePowerOnThreePhase.LastChanged < ocppConnectorState.CanHandlePowerOnOnePhase.LastChanged)
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

                    if ((ocppConnectorState.LastSetCurrent.Value > 0)
                        && (ocppConnectorState.IsCarFullyCharged.Value == true))
                    {
                        _logger.LogTrace("Do not try to start charging as last set Current is greater than 0 and car is fully charged");
                        return (0, 0);
                    }

                    var result = await _ocppChargePointActionService.StartCharging(chargingConnectorId!.Value, currentToStartChargingWith, canChangePhases ? phasesToStartChargingWith : null, cancellationToken).ConfigureAwait(false);
                    // Decision: result.HasError?
                    _logger.LogTrace("{method} decision: StartCharging result.HasError = {hasError}", nameof(SetLoadPointPower), result.HasError);
                    if (result.HasError)
                    {
                        _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                            chargingConnectorId, result.ErrorMessage);
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
                    car!.ShouldStartCharging.Value,
                    car.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn());

                if ((carChargeMode == ChargeModeV2.Manual)
                    || (carChargeMode == ChargeModeV2.Off)
                    || (car.ShouldStartCharging.Value == false)
                    || (car.ShouldStartCharging.LastChanged > (currentDate - _configurationWrapper.TimespanUntilSwitchOn()))
                    || (maxSoc <= car.SoC))
                {
                    return (0, 0);
                }
            }
            else
            {
                // Decision: ShouldStartCharging and LastChanged threshold for OCPP
                _logger.LogTrace("{method} decision: ShouldStartCharging = {shouldStart}, LastChanged = {lastChanged}, threshold = {threshold}",
                    nameof(SetLoadPointPower),
                    ocppConnectorState!.ShouldStartCharging.Value,
                    ocppConnectorState.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn());

                if ((connectorChargeMode == ChargeModeV2.Manual)
                    || (connectorChargeMode == ChargeModeV2.Off)
                    || (ocppConnectorState.ShouldStartCharging.Value == false)
                    || (ocppConnectorState.ShouldStartCharging.LastChanged > (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
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
                if (carChargeMode == ChargeModeV2.Auto)
                {
                    var currentToChargeWith = powerToSet / voltage / car!.ActualPhases;
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

                    await _teslaService.SetAmp(car.Id, currentToChargeWith).ConfigureAwait(false);
                    var actuallySetPower = GetPowerAtPhasesAndCurrent(car.ActualPhases, currentToChargeWith);
                    return (actuallySetPower - powerBeforeChanges, currentToChargeWith - currentBeforeChanges);
                }
                else
                {
                    return (0, 0);
                }
            }
            else
            {
                if (connectorChargeMode == ChargeModeV2.Auto)
                {
                    var phasesToChargeWith = ocppConnectorState!.PhaseCount.Value;
                    if (phasesToChargeWith == default)
                    {
                        if (ocppConnectorState!.LastSetPhases.Value == default)
                        {
                            phasesToChargeWith = maxPhases.Value;
                        }
                        else
                        {
                            phasesToChargeWith = ocppConnectorState!.LastSetPhases.Value.Value;
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
                    var ampChangeResult = await _ocppChargePointActionService.SetChargingCurrent(chargingConnectorId!.Value, currentToChargeWith, canChangePhases ? phasesToChargeWith : null, cancellationToken).ConfigureAwait(false);
                    // Decision: ampChangeResult.HasError?
                    _logger.LogTrace("{method} decision: ampChangeResult.HasError = {hasError}", nameof(SetLoadPointPower), ampChangeResult.HasError);
                    if (ampChangeResult.HasError)
                    {
                        _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                            chargingConnectorId, ampChangeResult.ErrorMessage);
                        return (0, 0);
                    }

                    var actuallySetPower = GetPowerAtPhasesAndCurrent(phasesToChargeWith.Value, currentToChargeWith);
                    return (powerBeforeChanges - actuallySetPower, currentBeforeChanges - currentToChargeWith);
                }
                return (0, 0);

            }

            #endregion
        }

        _logger.LogError("No path meets all conditions, check data why not: {carID}, {connectorId}", carId, chargingConnectorId);
        return (0, 0);
    }


    private async Task<(int? minCurrent, int? maxCurrent, int? minPhases, int? maxPhases, bool useCarToManageChargingSpeed, bool canChangePhases, ChargeModeV2? carChargeMode, ChargeModeV2? connectorChargeMode, int? carMaxSoc)> GetMinMaxCurrentsAndPhases(int? carId, int? connectorId, CancellationToken cancellationToken)
    {
        int? minCurrent = null;
        int? maxCurrent = null;
        int? minPhases = null;
        int? maxPhases = null;
        ChargeModeV2? carChargeMode = null;
        ChargeModeV2? connectorChargeMode = null;
        int? carMaxSoc = null;
        //ToDo: Set this to false if car is no Tesla as soon as other car brands are supported
        var useCarToManageChargingSpeed = carId != default;
        var canChangePhases = false;
        if (carId != default)
        {
            var carConfigValues = await _context.Cars
                .Where(c => c.Id == carId.Value)
                .Select(c => new {
                    c.MinimumAmpere,
                    c.MaximumAmpere,
                    c.ChargeMode,
                    c.MaximumSoc,
                })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            minCurrent = carConfigValues.MinimumAmpere;
            maxCurrent = carConfigValues.MaximumAmpere;
            carChargeMode = carConfigValues.ChargeMode;
            var car = _settings.Cars.First(c => c.Id == carId);
            minPhases = car.ActualPhases;
            maxPhases = car.ActualPhases;
            carMaxSoc = carConfigValues.MaximumSoc;
        }

        if (connectorId != default)
        {
            var chargingConnectorConfigValues = await _context.OcppChargingStationConnectors
                .Where(c => c.Id == connectorId.Value)
                .Select(c => new
                {
                    c.MinCurrent,
                    c.MaxCurrent,
                    c.ConnectedPhasesCount,
                    c.AutoSwitchBetween1And3PhasesEnabled,
                    c.ChargeMode
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
            connectorChargeMode = chargingConnectorConfigValues.ChargeMode;
        }

        return (minCurrent, maxCurrent, minPhases, maxPhases, useCarToManageChargingSpeed, canChangePhases, carChargeMode, connectorChargeMode, carMaxSoc);
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

        overage = AddHomeBatterStateToPowerCalculation(overage);
        return overage + currentChargingPower;
    }

    private int AddHomeBatterStateToPowerCalculation(int overage)
    {
        var homeBatteryMinSoc = _configurationWrapper.HomeBatteryMinSoc();
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
    /// <param name="targetTimeUtc"></param>
    /// <param name="currentDate"></param>
    /// <param name="energyToCharge"></param>
    /// <param name="maxPhases"></param>
    /// <param name="maxCurrent"></param>
    /// <param name="carId"></param>
    /// <param name="chargingConnectorId"></param>
    /// <returns></returns>
    private DtoChargingSchedule? GenerateEarliestOrLatestPossibleChargingSchedule(
        DateTimeOffset? targetTimeUtc,
        DateTimeOffset currentDate,
        int energyToCharge,
        int maxPhases,
        int maxCurrent,
        int? carId,
        int? chargingConnectorId)
    {
        _logger.LogTrace("{method}({targetTimeUtc}, {currentDate}, {energyToCharge}, {maxPhases}, {maxCurrent}, {carId}, {chargingConnectorId})",
            targetTimeUtc, currentDate, energyToCharge, maxPhases, maxCurrent, targetTimeUtc, carId, chargingConnectorId);
        if (energyToCharge < 1)
        {
            return null;
        }
        var maxChargingPower = GetPowerAtPhasesAndCurrent(maxPhases, maxCurrent);

        var chargingDuration = CalculateChargingDuration(
            energyToCharge,
            maxChargingPower);

        if (targetTimeUtc == default)
        {
            return new DtoChargingSchedule(carId, chargingConnectorId)
            {
                ValidFrom = currentDate,
                ValidTo = currentDate + chargingDuration,
                ChargingPower = maxChargingPower,
            };
        }

        var startTime = targetTimeUtc.Value - chargingDuration;

        return new(carId, chargingConnectorId)
        {
            ValidFrom = startTime,
            ValidTo = targetTimeUtc.Value,
            ChargingPower = maxChargingPower,
        };
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

    private int CalculateEnergyToCharge(
        int chargingTargetSoc,
        int currentSoC,
        int usableEnergy)
    {
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
}
