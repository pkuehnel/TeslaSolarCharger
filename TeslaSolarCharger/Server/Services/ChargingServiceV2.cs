using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Enums;

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
    private readonly ITeslaService _teslaService;
    private readonly IShouldStartStopChargingCalculator _shouldStartStopChargingCalculator;
    private readonly IEnergyDataService _energyDataService;
    private readonly IValidFromToSplitter _validFromToSplitter;
    private readonly IPowerToControlCalculationService _powerToControlCalculationService;
    private readonly IBackendApiService _backendApiService;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly IIssueKeys _issueKeys;
    private readonly INotChargingWithExpectedPowerReasonHelper _notChargingWithExpectedPowerReasonHelper;
    private readonly ITargetChargingValueCalculationService _targetChargingValueCalculationService;

    public ChargingServiceV2(ILogger<ChargingServiceV2> logger,
        IConfigurationWrapper configurationWrapper,
        ILoadPointManagementService loadPointManagementService,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        IOcppChargePointActionService ocppChargePointActionService,
        ISettings settings,
        ITscOnlyChargingCostService tscOnlyChargingCostService,
        ITeslaService teslaService,
        IShouldStartStopChargingCalculator shouldStartStopChargingCalculator,
        IEnergyDataService energyDataService,
        IValidFromToSplitter validFromToSplitter,
        IPowerToControlCalculationService powerToControlCalculationService,
        IBackendApiService backendApiService,
        IErrorHandlingService errorHandlingService,
        IIssueKeys issueKeys,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper,
        ITargetChargingValueCalculationService targetChargingValueCalculationService)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _loadPointManagementService = loadPointManagementService;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _ocppChargePointActionService = ocppChargePointActionService;
        _settings = settings;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
        _teslaService = teslaService;
        _shouldStartStopChargingCalculator = shouldStartStopChargingCalculator;
        _energyDataService = energyDataService;
        _validFromToSplitter = validFromToSplitter;
        _powerToControlCalculationService = powerToControlCalculationService;
        _backendApiService = backendApiService;
        _errorHandlingService = errorHandlingService;
        _issueKeys = issueKeys;
        _notChargingWithExpectedPowerReasonHelper = notChargingWithExpectedPowerReasonHelper;
        _targetChargingValueCalculationService = targetChargingValueCalculationService;
    }

    public async Task SetNewChargingValues(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(SetNewChargingValues));
        if (!await _backendApiService.IsBaseAppLicensed(true))
        {
            _logger.LogError("Can not set new charging values as base app is not licensed");
            await _errorHandlingService.HandleError(nameof(ChargingServiceV2), nameof(SetNewChargingValues), "Base App not licensed",
                "Can not send commands to car as app is not licensed. Buy a subscription on <a href=\"https://solar4car.com/subscriptions\">Solar4Car Subscriptions</a> to use TSC",
                _issueKeys.BaseAppNotLicensed, null, null).ConfigureAwait(false);
            return;
        }
        await _errorHandlingService.HandleErrorResolved(_issueKeys.BaseAppNotLicensed, null).ConfigureAwait(false);
        await CalculateGeofences();
        await AddNoOcppConnectionReason(cancellationToken).ConfigureAwait(false);
        await SetCurrentOfNonChargingTeslasToMax().ConfigureAwait(false);
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        await SetCarChargingTargetsToFulFilled(currentDate).ConfigureAwait(false);
        var chargingLoadPoints = _loadPointManagementService.GetLoadPointsWithChargingDetails();
        var powerToControl = await _powerToControlCalculationService.CalculatePowerToControl(chargingLoadPoints.Select(l => l.ChargingPower).Sum(), _notChargingWithExpectedPowerReasonHelper, cancellationToken).ConfigureAwait(false);
        if (ShouldSkipPowerUpdatesDueToTooRecentAmpChanges(chargingLoadPoints, currentDate))
        {
            return;
        }
        var loadPointsToManage = await _loadPointManagementService.GetLoadPointsToManage().ConfigureAwait(false);
        AddNotChargingReasons(loadPointsToManage);

        var chargingSchedules = await GenerateChargingSchedules(currentDate, loadPointsToManage, cancellationToken).ConfigureAwait(false);
        OptimizeChargingSwitchTimes(chargingSchedules, loadPointsToManage, currentDate);
        _settings.ChargingSchedules = new ConcurrentBag<DtoChargingSchedule>(chargingSchedules);

        _logger.LogDebug("Final calculated power to control: {powerToControl}", powerToControl);
        var activeChargingSchedules = chargingSchedules.Where(s => s.ValidFrom <= currentDate).ToList();

        await _shouldStartStopChargingCalculator.UpdateShouldStartStopChargingTimes(powerToControl);

        var targetChargingValues = loadPointsToManage
            .OrderBy(l => l.ChargingPriority)
            .Select(l => new DtoTargetChargingValues(l))
            .ToList();

        await _targetChargingValueCalculationService.AppendTargetValues(targetChargingValues, activeChargingSchedules, currentDate, powerToControl, cancellationToken);
        foreach (var targetChargingValue in targetChargingValues)
        {
            if (targetChargingValue.TargetValues == default)
            {
                continue;
            }
            if (!(await SetChargingPowerOfOccpConnectorForCarManagedLoadpoint(targetChargingValue, currentDate, cancellationToken)
                    .ConfigureAwait(false)))
            {
                _logger.LogDebug("Skipping further processing for loadpoint {@loadPoint} as OCPP charging power could not be set.", targetChargingValue.LoadPoint);
                continue;
            }

            if (targetChargingValue.LoadPoint.ManageChargingPowerByCar)
            {
                _logger.LogTrace("Loadpoint {carId}, {connectorId} is managed by car", targetChargingValue.LoadPoint.CarId, targetChargingValue.LoadPoint.ChargingConnectorId);
                var carId = targetChargingValue.LoadPoint.CarId!.Value;
                if (targetChargingValue.TargetValues.StopCharging)
                {
                    await _teslaService.StopCharging(carId).ConfigureAwait(false);
                    continue;
                }

                if (targetChargingValue.TargetValues.StartCharging && (targetChargingValue.TargetValues.TargetCurrent != default))
                {
                    await _teslaService.StartCharging(carId, (int)targetChargingValue.TargetValues.TargetCurrent.Value)
                        .ConfigureAwait(false);
                    continue;
                }
                

                if (targetChargingValue.TargetValues.TargetCurrent != default)
                {
                    await _teslaService.SetAmp(carId, (int)targetChargingValue.TargetValues.TargetCurrent.Value).ConfigureAwait(false);
                    continue;
                }
                _logger.LogError("Invalid target values for car {carId}: {@targetValues}", carId, targetChargingValue);
            }
            else
            {
                var connectorId = targetChargingValue.LoadPoint.ChargingConnectorId!.Value;
                if (targetChargingValue.TargetValues.StopCharging)
                {
                    await _ocppChargePointActionService.StopCharging(connectorId, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (targetChargingValue.TargetValues.StartCharging && (targetChargingValue.TargetValues.TargetCurrent != default))
                {
                    await _ocppChargePointActionService.StartCharging(connectorId, targetChargingValue.TargetValues.TargetCurrent.Value, targetChargingValue.TargetValues.TargetPhases, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                if (targetChargingValue.TargetValues.TargetCurrent != default)
                {
                    await _ocppChargePointActionService.SetChargingCurrent(connectorId, targetChargingValue.TargetValues.TargetCurrent.Value, targetChargingValue.TargetValues.TargetPhases, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }
                _logger.LogError("Invalid target values for OCPP connector {connectorId}: {@targetValues}", connectorId, targetChargingValue);
            }
        }

        _notChargingWithExpectedPowerReasonHelper.UpdateReasonsInSettings();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="targetChargingValue"></param>
    /// <param name="currentDate"></param>
    /// <returns>Succeeded</returns>
    private async Task<bool> SetChargingPowerOfOccpConnectorForCarManagedLoadpoint(DtoTargetChargingValues targetChargingValue, DateTimeOffset currentDate, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({@targetChargingValue}, {currentDate})", nameof(SetChargingPowerOfOccpConnectorForCarManagedLoadpoint), targetChargingValue, currentDate);
        if (targetChargingValue.LoadPoint.ManageChargingPowerByCar
            && (targetChargingValue.LoadPoint.ChargingConnectorId != default)
            && (_settings.OcppConnectorStates.TryGetValue(targetChargingValue.LoadPoint.ChargingConnectorId.Value, out var ocppState)))
        {
            _logger.LogDebug("Loadpoint {carId}, {connectorId} is managed by car", targetChargingValue.LoadPoint.CarId, targetChargingValue.LoadPoint.ChargingConnectorId);
            if (!(ocppState.LastSetCurrent.Value >= targetChargingValue.TargetValues?.TargetCurrent))
            {
                _logger.LogDebug("OCPP connector {connectorId} current {current} is lower than target current {targetCurrent}. Set new current.", targetChargingValue.LoadPoint.ChargingConnectorId, ocppState.LastSetCurrent.Value, targetChargingValue.TargetValues?.TargetCurrent);
                var connectorConfig = await _context.OcppChargingStationConnectors
                    .Where(c => c.Id == targetChargingValue.LoadPoint.ChargingConnectorId.Value)
                    .Select(c => new
                    {
                        c.MaxCurrent,
                        c.ConnectedPhasesCount,
                        c.AutoSwitchBetween1And3PhasesEnabled,
                        c.PhaseSwitchCoolDownTimeSeconds,
                    })
                    .FirstAsync(cancellationToken).ConfigureAwait(false);
                if (ocppState.IsCharging.Value != true)
                {
                    _logger.LogDebug("OCPP connector {connectorId} is not charging.", targetChargingValue.LoadPoint.ChargingConnectorId);
                    if ((ocppState.LastSetPhases.Value != connectorConfig.ConnectedPhasesCount)
                        && (connectorConfig.AutoSwitchBetween1And3PhasesEnabled)
                        && (ocppState.IsCharging.LastChanged > currentDate.AddSeconds(-connectorConfig.PhaseSwitchCoolDownTimeSeconds ?? 0)))
                    {
                        var waitUntil = ocppState.IsCharging.LastChanged.Value.AddSeconds(connectorConfig.PhaseSwitchCoolDownTimeSeconds ?? 0);
                        _logger.LogTrace("Wait with charge start for connector {connectorId} until {waitUntil} for phase switch cooldown", targetChargingValue.LoadPoint.ChargingConnectorId, waitUntil);
                        return false;
                    }
                    _logger.LogDebug("OCPP connector {connectorId} is not charging. Start charging with max current {maxCurrent}.", targetChargingValue.LoadPoint.ChargingConnectorId, connectorConfig.MaxCurrent);
                    // Max current can not be null as otherwise target values would be null.
                    var result = await _ocppChargePointActionService.StartCharging(targetChargingValue.LoadPoint.ChargingConnectorId.Value,
                        (decimal)connectorConfig.MaxCurrent!, connectorConfig.AutoSwitchBetween1And3PhasesEnabled ? connectorConfig.ConnectedPhasesCount : null, cancellationToken);
                    if (result.HasError)
                    {
                        _logger.LogError("Could not start charging for ocpp connector {ocppConnectorId}", targetChargingValue.LoadPoint.ChargingConnectorId);
                        return false;
                    }
                }
                else
                {
                    _logger.LogDebug("OCPP connector {connectorId} is charging. Set charging current to {targetCurrent}.", targetChargingValue.LoadPoint.ChargingConnectorId, targetChargingValue.TargetValues?.TargetCurrent);
                    // Max current can not be null as otherwise target values would be null.
                    var result = await _ocppChargePointActionService.SetChargingCurrent(targetChargingValue.LoadPoint.ChargingConnectorId.Value,
                        (decimal)connectorConfig.MaxCurrent!, null, cancellationToken).ConfigureAwait(false);
                    if (result.HasError)
                    {
                        _logger.LogError("Could not start update charging current for ocpp connector {ocppConnectorId}", targetChargingValue.LoadPoint.ChargingConnectorId);
                        return false;
                    }
                }
            }
        }
        return true;
    }


    private bool ShouldSkipPowerUpdatesDueToTooRecentAmpChanges(List<DtoLoadPointWithCurrentChargingValues> chargingLoadPoints,
        DateTimeOffset currentDate)
    {
        var skipValueChanges = _configurationWrapper.SkipPowerChangesOnLastAdjustmentNewerThan();
        var earliestAmpChange = currentDate - skipValueChanges;
        var earliestPlugin = currentDate - (2 * skipValueChanges);
        foreach (var chargingLoadPoint in chargingLoadPoints)
        {
            if (chargingLoadPoint.CarId != default)
            {
                var car = _settings.Cars.First(c => c.Id == chargingLoadPoint.CarId.Value);
                var lastAmpChange = car.LastSetAmp.LastChanged;
                if (lastAmpChange > earliestAmpChange)
                {
                    _logger.LogWarning("Skipping amp changes as Car {carId}'s last amp change {lastAmpChange} is newer than {skipValueChanges}.", chargingLoadPoint.CarId, car.LastSetAmp.LastChanged, earliestAmpChange);
                    return true;
                }

                var lastPlugIn = car.LastPluggedIn;
                if ((car.PluggedIn == true) && (lastPlugIn > earliestPlugin) && (car.ChargerRequestedCurrent > car.ChargerActualCurrent))
                {
                    _logger.LogWarning("Skipping amp changes as Car {carId} was plugged in after {earliestPlugIn}.", chargingLoadPoint.CarId, earliestPlugin);
                    return true;
                }
            }

            if (chargingLoadPoint.ChargingConnectorId != default)
            {
                var connectorState = _settings.OcppConnectorStates.GetValueOrDefault(chargingLoadPoint.ChargingConnectorId.Value);
                if (connectorState != default)
                {
                    if (connectorState.LastSetCurrent.LastChanged > earliestAmpChange)
                    {
                        _logger.LogWarning("Skipping amp changes as Charging Connector {chargingConnectorId}'s last amp change {lastAmpChange} is newer than {skipValueChanges}.", chargingLoadPoint.ChargingConnectorId, connectorState.LastSetCurrent.LastChanged, earliestAmpChange);
                        return true;
                    }

                    if (connectorState.IsPluggedIn.Value
                        && (connectorState.IsPluggedIn.LastChanged > earliestPlugin)
                        && (connectorState.ChargingCurrent.Value > 0)
                        && (connectorState.ChargingCurrent.Value < connectorState.LastSetCurrent.Value))
                    {
                        _logger.LogWarning("Skipping amp changes as Charging Connector {chargingConnectorId} was plugged in after {earliestPlugin}.", chargingLoadPoint.ChargingConnectorId, earliestPlugin);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private async Task AddNoOcppConnectionReason(CancellationToken cancellationToken)
    {
        var chargingConnectorIdsToManage = await _context.OcppChargingStationConnectors
            .Where(c => c.ShouldBeManaged)
            .Select(c => c.Id)
            .ToHashSetAsync(cancellationToken).ConfigureAwait(false);

        foreach (var connectorId in chargingConnectorIdsToManage)
        {
            if (!_settings.OcppConnectorStates.ContainsKey(connectorId))
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(null, connectorId, new("OCPP connection not established. After a TSC or charger reboot it can take up to 5 minutes until the charger is connected again."));
            }
        }
    }

    private void AddNotChargingReasons(List<DtoLoadPointOverview> loadPointsToManage)
    {
        foreach (var dtoCar in _settings.CarsToManage)
        {
            if (dtoCar.IsHomeGeofence != true)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(dtoCar.Id, null, new("Car is not at home"));
            }

            if (dtoCar.PluggedIn != true)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(dtoCar.Id, null, new("Car is not plugged in"));
            }
        }

        foreach (var settingsOcppConnectorState in _settings.OcppConnectorStates)
        {
            if (!settingsOcppConnectorState.Value.IsPluggedIn.Value)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(null, settingsOcppConnectorState.Key, new("Charging connector is not plugged in"));
            }

            if (settingsOcppConnectorState.Value.IsCarFullyCharged.Value == true)
            {
                var loadPoint = loadPointsToManage.FirstOrDefault(l => l.ChargingConnectorId == settingsOcppConnectorState.Key);
                if ((loadPoint == default) || (!loadPoint.ManageChargingPowerByCar))
                {
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(null, settingsOcppConnectorState.Key, new("Charging stopped by car, e.g. it is full or its charge limit is reached."));
                }
            }
        }
    }

    private async Task SetCarChargingTargetsToFulFilled(DateTimeOffset currentDate)
    {
        _logger.LogTrace("{method}({currentDate})", nameof(SetCarChargingTargetsToFulFilled), currentDate);
        var carSocs = _settings.CarsToManage.ToDictionary(c => c.Id, c => c.SoC);
        var carIds = carSocs.Keys.ToHashSet();
        var chargingTargets = await _context.CarChargingTargets
            .Where(c => carIds.Contains(c.CarId))
            .ToListAsync().ConfigureAwait(false);
        foreach (var chargingTarget in chargingTargets)
        {
            if (carSocs[chargingTarget.CarId] >= chargingTarget.TargetSoc)
            {
                chargingTarget.LastFulFilled = currentDate;
            }
        }
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    private void OptimizeChargingSwitchTimes(List<DtoChargingSchedule> chargingSchedules,
        List<DtoLoadPointOverview> loadPointsToManage, DateTimeOffset currentDate)
    {
        _logger.LogTrace("{method}({chargingSchedules}, {loadpointsToManage})", nameof(OptimizeChargingSwitchTimes), chargingSchedules, loadPointsToManage);
        var timespanToCombineCharges = TimeSpan.FromMinutes(20);
        foreach (var dtoLoadPointOverview in loadPointsToManage)
        {
            if ((!(dtoLoadPointOverview.ChargingPower > 0))
                || (dtoLoadPointOverview.ActualPhases == default)
                || (dtoLoadPointOverview.MinCurrent == default))
            {
                continue;
            }
            var correspondingChargingSchedules = chargingSchedules
                .Where(c => c.CarId == dtoLoadPointOverview.CarId
                            && c.OccpChargingConnectorId == dtoLoadPointOverview.ChargingConnectorId)
                .OrderBy(c => c.ValidFrom)
                .ToList();
            if (correspondingChargingSchedules.Count < 1)
            {
                continue;
            }

            var nextChargingSchedule = correspondingChargingSchedules.First();
            if (nextChargingSchedule.ValidFrom <= currentDate)
            {
                continue;
            }
            if (nextChargingSchedule.ValidFrom > currentDate.Add(timespanToCombineCharges))
            {
                continue;
            }
            _logger.LogDebug("Less than {minimumTime} until next charging schedule {@nextChargingSchedule}. Bridge time with minimum power.", timespanToCombineCharges, nextChargingSchedule);
            var addedTime = nextChargingSchedule.ValidFrom - currentDate;
            var minPower = GetPowerAtPhasesAndCurrent(dtoLoadPointOverview.ActualPhases.Value, dtoLoadPointOverview.MinCurrent.Value, dtoLoadPointOverview.EstimatedVoltageWhileCharging);
            var addedEnergy = addedTime.TotalHours * minPower;
            var nextChargingScheduleDuration = nextChargingSchedule.ValidTo - nextChargingSchedule.ValidFrom;
            var powerToReduce = addedEnergy / nextChargingScheduleDuration.TotalHours;
            var resultingPower = nextChargingSchedule.ChargingPower - powerToReduce;
            if (resultingPower < minPower)
            {
                _logger.LogDebug("Resulting power {resultingPower} is below minimum power {minPower}. Using minimum power.", resultingPower, minPower);
                nextChargingSchedule.ChargingPower = minPower;
            }
            else
            {
                _logger.LogDebug("Setting resulting power to {resultingPower}", resultingPower);
                nextChargingSchedule.ChargingPower = (int)resultingPower;
            }

            var bridgeChargingSchedule = new DtoChargingSchedule(dtoLoadPointOverview.CarId, dtoLoadPointOverview.ChargingConnectorId)
            {
                ValidFrom = currentDate,
                ValidTo = nextChargingSchedule.ValidFrom,
                ChargingPower = minPower,
            };
            _logger.LogDebug("Adding bridge charging schedule {@bridgeChargingSchedule}", bridgeChargingSchedule);
            chargingSchedules.Add(bridgeChargingSchedule);
        }


    }

    private async Task SetCurrentOfNonChargingTeslasToMax()
    {
        _logger.LogTrace("{method}()", nameof(SetCurrentOfNonChargingTeslasToMax));
        var carsToSetToMaxCurrent = _settings.CarsToManage
            .Where(c => (c.State == CarStateEnum.Online)
                        && (c.IsHomeGeofence == true)
                        && (c.PluggedIn == true)
                        && (c.ChargerRequestedCurrent != c.MaximumAmpere)
                        && (c.ChargerPilotCurrent > c.ChargerRequestedCurrent)
                        && (c.ChargeModeV2 == ChargeModeV2.Auto))
            .ToList();

        foreach (var car in carsToSetToMaxCurrent)
        {
            _logger.LogDebug("Set current of car {carId} to max as is not charging", car.Id);
            await _teslaService.SetAmp(car.Id, car.MaximumAmpere).ConfigureAwait(false);
        }
    }

    private async Task<List<DtoChargingSchedule>> GenerateChargingSchedules(DateTimeOffset currentDate, List<DtoLoadPointOverview> loadPointsToManage,
        CancellationToken cancellationToken)
    {
        ToDo: Does not charge until soc is reached but stops as soon as time is over
        _logger.LogTrace("{method}({currentDate}, {loadPointsToManage})", nameof(GenerateChargingSchedules), currentDate, loadPointsToManage.Count);
        var chargingSchedules = new List<DtoChargingSchedule>();
        foreach (var loadpoint in loadPointsToManage)
        {
            if (loadpoint.CarId != default)
            {
                var car = _settings.Cars.First(c => c.Id == loadpoint.CarId.Value);
                if (car.ChargeModeV2 != ChargeModeV2.Auto)
                {
                    continue;
                }
                var (carUsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent) = await GetChargingScheduleRelevantData(loadpoint.CarId, loadpoint.ChargingConnectorId).ConfigureAwait(false);
                if (carUsableEnergy == default || carSoC == default || maxPhases == default || maxCurrent == default)
                {
                    _logger.LogWarning("Can not schedule charging as at least one required value is unknown.");
                    continue;
                }

                var nextTarget = await GetRelevantTarget(car.Id, currentDate, cancellationToken).ConfigureAwait(false);
                if (nextTarget != default)
                {
                    var energyToCharge = CalculateEnergyToCharge(
                        nextTarget.TargetSoc,
                        car.SoC ?? 0,
                        carUsableEnergy.Value);
                    var maxPower = GetPowerAtPhasesAndCurrent(maxPhases.Value, maxCurrent.Value, loadpoint.EstimatedVoltageWhileCharging);
                    if (nextTarget.NextExecutionTime < currentDate)
                    {
                        _logger.LogWarning("Next target {nextTarget} is in the past. Plan charging immediatly.", nextTarget);
                        var chargingDuration = CalculateChargingDuration(energyToCharge, maxPower);
                        chargingSchedules.Add(new DtoChargingSchedule(loadpoint.CarId, loadpoint.ChargingConnectorId)
                        {
                            ValidFrom = currentDate,
                            ValidTo = currentDate + chargingDuration,
                            ChargingPower = maxPower,
                        });
                        continue;
                    }
                    if (_configurationWrapper.UsePredictedSolarPowerGenerationForChargingSchedules()
                        && minPhases != default
                        && minCurrent != default)
                    {
                        var currentFullHour = new DateTimeOffset(currentDate.Year, currentDate.Month, currentDate.Day, currentDate.Hour, 0, 0, currentDate.Offset);
                        var surplusTimeSpanInHours = 1;
                        var fullHourAfterNextTarget = new DateTimeOffset(nextTarget.NextExecutionTime.Year, nextTarget.NextExecutionTime.Month, nextTarget.NextExecutionTime.Day, nextTarget.NextExecutionTime.Hour + surplusTimeSpanInHours, 0, 0, nextTarget.NextExecutionTime.Offset);
                        var predictedSurplusSlices = await _energyDataService
                            .GetPredictedSurplusPerSlice(currentFullHour, fullHourAfterNextTarget, TimeSpan.FromHours(surplusTimeSpanInHours), cancellationToken)
                            .ConfigureAwait(false);
                        var minPower = GetPowerAtPhasesAndCurrent(minPhases.Value, minCurrent.Value, loadpoint.EstimatedVoltageWhileCharging);
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

    private int GetPowerAtPhasesAndCurrent(int phases, decimal current, int? voltage)
    {
        return (int)(phases * current * (voltage ?? 230));
    }


    private async Task<DtoTimeZonedChargingTarget?> GetRelevantTarget(int carId, DateTimeOffset currentDate,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({carId}, {currentDate})", nameof(GetRelevantTarget), carId, currentDate);
        var car = _settings.Cars.First(c => c.Id == carId);
        var lastPluggedIn = car.PluggedIn == true ? (car.LastPluggedIn ?? currentDate) : currentDate;
        var unfulFilledChargingTargets = await _context.CarChargingTargets
            .Where(c => c.CarId == carId
                        && (!(c.LastFulFilled >= currentDate)))
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (unfulFilledChargingTargets.Count < 1)
        {
            _logger.LogDebug("No charging targets found for car {carId}.", carId);
            return null;
        }
        DtoTimeZonedChargingTarget? nextTarget = null;
        foreach (var carChargingTarget in unfulFilledChargingTargets)
        {
            var nextTargetUtc = GetNextTargetUtc(carChargingTarget, lastPluggedIn);
            if ((nextTargetUtc != default)
                    && ((nextTarget == default)
                        || (nextTargetUtc < nextTarget.NextExecutionTime)))
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
                    NextExecutionTime = nextTargetUtc.Value,
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
    /// <param name="voltage"></param>
    /// <returns></returns>
    private DtoChargingSchedule? GenerateEarliestOrLatestPossibleChargingSchedule(
        DateTimeOffset? targetTimeUtc,
        DateTimeOffset currentDate,
        int energyToCharge,
        int maxPhases,
        int maxCurrent,
        int? carId,
        int? chargingConnectorId,
        int? voltage)
    {
        _logger.LogTrace("{method}({targetTimeUtc}, {currentDate}, {energyToCharge}, {maxPhases}, {maxCurrent}, {carId}, {chargingConnectorId})",
            targetTimeUtc, currentDate, energyToCharge, maxPhases, maxCurrent, targetTimeUtc, carId, chargingConnectorId);
        if (energyToCharge < 1)
        {
            return null;
        }
        var maxChargingPower = GetPowerAtPhasesAndCurrent(maxPhases, maxCurrent, voltage);

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

    private TimeSpan CalculateChargingDuration(
        int energyToChargeWh,
        double maxChargingPowerW)
    {
        // hours = Wh / W
        return TimeSpan.FromHours(energyToChargeWh / maxChargingPowerW);
    }

    internal DateTimeOffset? GetNextTargetUtc(CarChargingTarget chargingTarget, DateTimeOffset lastPluggedIn)
    {
        var tz = string.IsNullOrWhiteSpace(chargingTarget.ClientTimeZone)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(chargingTarget.ClientTimeZone);

        var earliestExecutionTime = TimeZoneInfo.ConvertTime(lastPluggedIn, tz);

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

        return null;
    }
}
