﻿using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Resources;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
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
        _notChargingWithExpectedPowerReasonHelper = notChargingWithExpectedPowerReasonHelper;
        _targetChargingValueCalculationService = targetChargingValueCalculationService;
    }

    public async Task SetNewChargingValues(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(SetNewChargingValues));
        if ((await _backendApiService.IsBaseAppLicensed(true)).Data != true)
        {
            _logger.LogError("Can not set new charging values as base app is not licensed");
            return;
        }
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        await CalculateGeofences(currentDate);
        await AddNoOcppConnectionReason(cancellationToken).ConfigureAwait(false);
        await SetCurrentOfNonChargingTeslasToMax().ConfigureAwait(false);
        //Needs to be after setting Teslas to max current as otherwise the max current of Teslas is not determined correctly
        await AutodetectCarCapabilities(currentDate, cancellationToken).ConfigureAwait(false);
        await SetCarChargingTargetsToFulFilled(currentDate).ConfigureAwait(false);
        var loadPointsToManage = await _loadPointManagementService.GetLoadPointsToManage().ConfigureAwait(false);
        var chargingLoadPoints = await _loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var powerToControl = await _powerToControlCalculationService.CalculatePowerToControl(chargingLoadPoints, _notChargingWithExpectedPowerReasonHelper, cancellationToken).ConfigureAwait(false);
        if (ShouldSkipPowerUpdatesDueToTooRecentAmpChangesOrPlugin(chargingLoadPoints, currentDate))
        {
            return;
        }

        AddNotChargingReasons();
        //reduce current for the rest loadpoints as this loadpoint might start charging with max current
        var loadpointInCarCapabilityDetection = new List<DtoLoadPointOverview>();
        foreach (var loadpoint in loadPointsToManage)
        {
            if (loadpoint.ChargingConnectorId == default)
            {
                continue;
            }
            var stateAvailable = _settings.OcppConnectorStates.TryGetValue(loadpoint.ChargingConnectorId.Value, out var state);
            if (!stateAvailable)
            {
                continue;
            }
            if (((state!.CarCapabilities.Value == default) || (state.CarCapabilities.Timestamp < state.IsPluggedIn.LastChanged))
                && state.IsCarFullyCharged.Value != true)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId, new("Charging with full speed for autodetection of connected car's charging speed. This is a normal behaviour right after plugin and will stop automatically."));
                loadpointInCarCapabilityDetection.Add(loadpoint);
            }
        }

        var chargingSchedules = await GenerateChargingSchedules(currentDate, loadPointsToManage, cancellationToken).ConfigureAwait(false);
        OptimizeChargingSwitchTimes(chargingSchedules, loadPointsToManage, currentDate);
        _settings.ChargingSchedules = new ConcurrentBag<DtoChargingSchedule>(chargingSchedules);

        _logger.LogDebug("Final calculated power to control: {powerToControl}", powerToControl);
        var activeChargingSchedules = chargingSchedules.Where(s => s.ValidFrom <= currentDate).ToList();

        await _shouldStartStopChargingCalculator.UpdateShouldStartStopChargingTimes(powerToControl);

        var targetChargingValues = loadPointsToManage
            //Do not set target values for loadpoints that are in car capability detection as these are set to max current
            .Where(l => l.ChargingConnectorId != default || !loadpointInCarCapabilityDetection.Any(lp => lp.ChargingConnectorId == l.ChargingConnectorId))
            .OrderBy(l => l.ChargingPriority)
            .Select(l => new DtoTargetChargingValues(l))
            .ToList();

        await _targetChargingValueCalculationService.AppendTargetValues(targetChargingValues, activeChargingSchedules, currentDate, powerToControl, loadpointInCarCapabilityDetection.Sum(l => l.MaxCurrent ?? 0), cancellationToken);
        foreach (var targetChargingValue in targetChargingValues)
        {
            if (targetChargingValue.TargetValues == default)
            {
                continue;
            }
            //Next line sets target power OCPP stations if loadpoint is managed by car. Returns true if power does not need to be set or if it was set successfully.
            //Returns false if power should be set but did not succeed, e.g. OCPP connection is not established.
            if (!(await SetChargingPowerOfOccpConnectorForCarManagedLoadpoint(targetChargingValue, currentDate, cancellationToken)
                    .ConfigureAwait(false)))
            {
                _logger.LogDebug("Skipping further processing for loadpoint {@loadPoint} as OCPP charging power could not be set.", targetChargingValue.LoadPoint);
                continue;
            }

            //Round as sometimes 4.9999999999991 is the result and this would reduce current by 1A on cars with 1A step size.
            if (targetChargingValue.TargetValues.TargetCurrent != default)
            {
                targetChargingValue.TargetValues.TargetCurrent = Math.Round(targetChargingValue.TargetValues.TargetCurrent.Value, 1);
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

        await _notChargingWithExpectedPowerReasonHelper.UpdateReasonsInSettings().ConfigureAwait(false);
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
                if (!await SetChargingConnectorToMaxPowerAndMaxPhases(targetChargingValue.LoadPoint.ChargingConnectorId.Value, currentDate, cancellationToken, ocppState).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private async Task<bool> SetChargingConnectorToMaxPowerAndMaxPhases(int chargingConnectorId,
        DateTimeOffset currentDate, CancellationToken cancellationToken, DtoOcppConnectorState ocppState)
    {
        var connectorConfig = await _context.OcppChargingStationConnectors
            .Where(c => c.Id == chargingConnectorId)
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
            _logger.LogDebug("OCPP connector {connectorId} is not charging.", chargingConnectorId);
            if ((ocppState.LastSetPhases.Value != connectorConfig.ConnectedPhasesCount)
                && (connectorConfig.AutoSwitchBetween1And3PhasesEnabled)
                && (ocppState.IsCharging.LastChanged > currentDate.AddSeconds(-connectorConfig.PhaseSwitchCoolDownTimeSeconds ?? 0)))
            {
                var waitUntil = ocppState.IsCharging.LastChanged.Value.AddSeconds(connectorConfig.PhaseSwitchCoolDownTimeSeconds ?? 0);
                _logger.LogTrace("Wait with charge start for connector {connectorId} until {waitUntil} for phase switch cooldown", chargingConnectorId, waitUntil);
                return false;
            }
            _logger.LogDebug("OCPP connector {connectorId} is not charging. Start charging with max current {maxCurrent}.", chargingConnectorId, connectorConfig.MaxCurrent);
            // Max current can not be null as otherwise target values would be null.
            var result = await _ocppChargePointActionService.StartCharging(chargingConnectorId,
                (decimal)connectorConfig.MaxCurrent!, connectorConfig.AutoSwitchBetween1And3PhasesEnabled ? connectorConfig.ConnectedPhasesCount : null, cancellationToken);
            if (result.HasError)
            {
                _logger.LogError("Could not start charging for ocpp connector {ocppConnectorId}", chargingConnectorId);
                return false;
            }
        }
        else
        {
            _logger.LogDebug("OCPP connector {connectorId} is charging. Set charging current to {targetCurrent}.", chargingConnectorId, connectorConfig.MaxCurrent);
            // Max current can not be null as otherwise target values would be null.
            var result = await _ocppChargePointActionService.SetChargingCurrent(chargingConnectorId,
                (decimal)connectorConfig.MaxCurrent!, null, cancellationToken).ConfigureAwait(false);
            if (result.HasError)
            {
                _logger.LogError("Could not start update charging current for ocpp connector {ocppConnectorId}", chargingConnectorId);
                return false;
            }
        }

        return true;
    }


    private bool ShouldSkipPowerUpdatesDueToTooRecentAmpChangesOrPlugin(List<DtoLoadPointWithCurrentChargingValues> chargingLoadPoints,
        DateTimeOffset currentDate)
    {
        var skipValueChanges = _configurationWrapper.SkipPowerChangesOnLastAdjustmentNewerThan();
        var earliestAmpChange = currentDate - skipValueChanges;
        var earliestPlugin = currentDate - (2 * skipValueChanges);
        foreach (var chargingLoadPoint in chargingLoadPoints)
        {
            if (_powerToControlCalculationService.HasTooLateChanges(chargingLoadPoint, earliestAmpChange, earliestPlugin))
            {
                return true;
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

    private void AddNotChargingReasons()
    {
        foreach (var dtoCar in _settings.CarsToManage)
        {
            if (dtoCar.IsHomeGeofence.Value != true)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(dtoCar.Id, null, new("Car is not at home"));
            }

            if (dtoCar.PluggedIn.Value != true)
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
        }
    }

    private async Task SetCarChargingTargetsToFulFilled(DateTimeOffset currentDate)
    {
        _logger.LogTrace("{method}({currentDate})", nameof(SetCarChargingTargetsToFulFilled), currentDate);
        var carIds = _settings.CarsToManage.Select(c => c.Id).ToHashSet();
        var chargingTargets = await _context.CarChargingTargets
            .Where(c => carIds.Contains(c.CarId))
            .ToListAsync().ConfigureAwait(false);
        foreach (var chargingTarget in chargingTargets)
        {
            var car = _settings.Cars.First(c => c.Id == chargingTarget.CarId);
            var actualTargetSoc = GetActualTargetSoc(car.SocLimit.Value, chargingTarget.TargetSoc, car.IsCharging.Value == true);
            if (car.SoC.Value >= actualTargetSoc || car.PluggedIn.Value != true || car.IsHomeGeofence.Value != true)
            {
                chargingTarget.LastFulFilled = currentDate;
            }
        }
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Adds +1 to the target SOC if the car side SOC limit is equal to the charging target SOC to force the car to stop charging by itself.
    /// </summary>
    private int GetActualTargetSoc(int? carSideSocLimit, int chargingTargetTargetSoc, bool isCurrentlyCharging)
    {
        _logger.LogTrace("{method}({carSideSocLimit}, {chargingTargetTargetSoc})", nameof(GetActualTargetSoc), carSideSocLimit, chargingTargetTargetSoc);
        if ((carSideSocLimit == chargingTargetTargetSoc) && isCurrentlyCharging)
        {
            _logger.LogDebug("Car side SOC limit {carSideSocLimit} is equal to charging target SOC {chargingTargetTargetSoc} and car is currently charging. Incrementing target SOC by 1 to force car to stop charging by itself.", carSideSocLimit, chargingTargetTargetSoc);
            return chargingTargetTargetSoc + 1;
        }
        return chargingTargetTargetSoc;
    }

    private void OptimizeChargingSwitchTimes(List<DtoChargingSchedule> chargingSchedules,
        List<DtoLoadPointOverview> loadPointsToManage, DateTimeOffset currentDate)
    {
        _logger.LogTrace("{method}({@chargingSchedules}, {loadpointsToManage})", nameof(OptimizeChargingSwitchTimes), chargingSchedules, loadPointsToManage);
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
                            && c.OcppChargingConnectorId == dtoLoadPointOverview.ChargingConnectorId)
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
            .Where(c => (c.IsOnline.Value == true)
                        && (c.IsHomeGeofence.Value == true)
                        && (c.PluggedIn.Value == true)
                        && (c.ChargerRequestedCurrent.Value != c.MaximumAmpere)
                        && (c.ChargerPilotCurrent.Value > c.ChargerRequestedCurrent.Value)
                        && (c.IsCharging.Value == false)
                        && (c.ChargeModeV2 == ChargeModeV2.Auto))
            .ToList();

        foreach (var car in carsToSetToMaxCurrent)
        {
            _logger.LogDebug("Set current of car {carId} to max as is not charging", car.Id);
            await _teslaService.SetAmp(car.Id, car.MaximumAmpere).ConfigureAwait(false);
        }
    }

    private async Task AutodetectCarCapabilities(DateTimeOffset currentDate, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(AutodetectCarCapabilities));
        foreach (var ocppConnectorState in _settings.OcppConnectorStates)
        {
            if (ocppConnectorState.Value.IsPluggedIn.Value
                && (ocppConnectorState.Value.CarCapabilities.Value == default
                    || (ocppConnectorState.Value.CarCapabilities.Timestamp < ocppConnectorState.Value.IsPluggedIn.LastChanged)))
            {
                _logger.LogTrace("Setting charging connector {chargingConnectorId} to max power to detect car capabilities", ocppConnectorState.Key);
                await SetChargingConnectorToMaxPowerAndMaxPhases(ocppConnectorState.Key, currentDate, cancellationToken, ocppConnectorState.Value);
            }
            var skipValueChanges = _configurationWrapper.SkipPowerChangesOnLastAdjustmentNewerThan();
            var earliestPlugin = currentDate - (2 * skipValueChanges);
            if ((ocppConnectorState.Value.LastSetCurrent.LastChanged < earliestPlugin)
                && (ocppConnectorState.Value.CarCapabilities.Value == default
                    || (ocppConnectorState.Value.CarCapabilities.Timestamp < ocppConnectorState.Value.IsPluggedIn.LastChanged)))
            {
                _logger.LogTrace("Detecting car capabilities for charging connector {chargingConnectorId}", ocppConnectorState.Key);
                //Set car capabilities
                var maxCurrent = ocppConnectorState.Value.ChargingCurrent.Value;
                var phases = ocppConnectorState.Value.PhaseCount.Value;
                if (maxCurrent > 0 && phases != default)
                {
                    ocppConnectorState.Value.CarCapabilities.Update(currentDate,
                        new DtoCarCapabilities() { MaxCurrent = maxCurrent, MaxPhases = phases.Value });
                }
            }
        }
    }

    private async Task<List<DtoChargingSchedule>> GenerateChargingSchedules(DateTimeOffset currentDate, List<DtoLoadPointOverview> loadPointsToManage,
        CancellationToken cancellationToken)
    {
        //ToDo: Does not charge until soc is reached but stops as soon as time is over
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
                    var actualTargetSoc = GetActualTargetSoc(car.SocLimit.Value, nextTarget.TargetSoc, car.IsCharging.Value == true);
                    var energyToCharge = CalculateEnergyToCharge(
                        actualTargetSoc,
                        car.SoC.Value ?? 0,
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
                    var relevantSplittedChargingSchedules = splittedChargingSchedules
                        .Where(cs => cs.CarId == loadpoint.CarId && cs.OcppChargingConnectorId == loadpoint.ChargingConnectorId)
                        .ToList();
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

                        var correspondingChargingSchedule = relevantSplittedChargingSchedules
                            .FirstOrDefault(cs => cs.ValidFrom == gridPriceOrderedElectricityPrice.ValidFrom
                                                  && cs.ValidTo == gridPriceOrderedElectricityPrice.ValidTo);
                        if (correspondingChargingSchedule == default)
                        {
                            correspondingChargingSchedule = new DtoChargingSchedule(loadpoint.CarId.Value, loadpoint.ChargingConnectorId)
                            {
                                ValidFrom = gridPriceOrderedElectricityPrice.ValidFrom,
                                ValidTo = gridPriceOrderedElectricityPrice.ValidTo,
                            };
                            relevantSplittedChargingSchedules.Add(correspondingChargingSchedule);
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

                    if (relevantSplittedChargingSchedules.Any())
                    {
                        chargingSchedules.RemoveAll(cs =>
                            cs.CarId == loadpoint.CarId &&
                            cs.OcppChargingConnectorId == loadpoint.ChargingConnectorId &&
                            cs.ValidFrom < nextTarget.NextExecutionTime &&
                            cs.ValidTo > currentDate);

                        chargingSchedules.AddRange(relevantSplittedChargingSchedules);
                    }

                    if (remainingEnergyToCoverFromGrid > 0)
                    {
                        var lastChargingSchedule = chargingSchedules
                            .Where(c => c.CarId == loadpoint.CarId && c.OcppChargingConnectorId == loadpoint.ChargingConnectorId)
                            .OrderByDescending(c => c.ValidTo)
                            .FirstOrDefault();
                        if (lastChargingSchedule != default)
                        {
                            _logger.LogDebug("Last charging schedule {@lastChargingSchedule} is not enough to cover remaining energy {remainingEnergyToCoverFromGrid}. Extend it.", lastChargingSchedule, remainingEnergyToCoverFromGrid);
                            var chargingDuration = CalculateChargingDuration(remainingEnergyToCoverFromGrid, lastChargingSchedule.ChargingPower);
                            lastChargingSchedule.ValidTo += chargingDuration;
                        }
                    }
                }
            }
        }

        return chargingSchedules;
    }

    private async Task CalculateGeofences(DateTimeOffset currentDate)
    {
        _logger.LogTrace("{method}()", nameof(CalculateGeofences));
        foreach (var car in _settings.CarsToManage)
        {
            if (car.Longitude.Value == null || car.Latitude.Value == null)
            {
                _logger.LogDebug("No location data for car {carId}. Do not calculate geofence", car.Id);
                car.DistanceToHomeGeofence.Update(currentDate, null);
                continue;
            }

            var homeDetectionVia = await _context.Cars
                .Where(c => c.Id == car.Id)
                .Select(c => c.HomeDetectionVia)
                .FirstAsync();

            if (homeDetectionVia != HomeDetectionVia.GpsLocation)
            {
                _logger.LogDebug("Car {carId} uses fleet telemetry but does not include tracking relevant fields. Do not calculate geofence", car.Id);
                car.DistanceToHomeGeofence.Update(currentDate, null);
                continue;
            }

            var distance = GetDistance(car.Longitude.Value.Value, car.Latitude.Value.Value,
                _configurationWrapper.HomeGeofenceLongitude(), _configurationWrapper.HomeGeofenceLatitude());
            _logger.LogDebug("Calculated distance to home geofence for car {carId}: {calculatedDistance}", car.Id, distance);
            var radius = _configurationWrapper.HomeGeofenceRadius();
            var wasAtHomeBefore = car.IsHomeGeofence;
            car.IsHomeGeofence.Update(currentDate, distance < radius);
            if (wasAtHomeBefore != car.IsHomeGeofence)
            {
                await _loadPointManagementService.CarStateChanged(car.Id);
            }
            car.DistanceToHomeGeofence.Update(currentDate, (int)distance - radius);
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
        var lastPluggedIn = car.PluggedIn.Value == true ? (car.PluggedIn.LastChanged ?? currentDate) : currentDate;
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
        var carSoC = carSetting?.SoC.Value;
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
        _logger.LogTrace("{method}({@chargingTarget}, {lastPluggedIn})", nameof(GetNextTargetUtc), chargingTarget, lastPluggedIn);
        var tz = string.IsNullOrWhiteSpace(chargingTarget.ClientTimeZone)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(chargingTarget.ClientTimeZone);

        var earliestExecutionTime = TimeZoneInfo.ConvertTime(lastPluggedIn, tz);
        _logger.LogTrace("Earliest execution time for target {targetId} is {earliestExecutionTime} in timezone {tz}.", chargingTarget.Id, earliestExecutionTime, tz.Id);

        DateTimeOffset? candidate;

        if (chargingTarget.TargetDate.HasValue)
        {
            candidate = new DateTimeOffset(chargingTarget.TargetDate.Value, chargingTarget.TargetTime,
                tz.GetUtcOffset(new(chargingTarget.TargetDate.Value, chargingTarget.TargetTime)));
            _logger.LogTrace("Candidate: {candidate} for target {targetId} in timezone {tz}.", candidate, chargingTarget.Id, tz.Id);

            //candidate is irrelevant if it is in the past
            if (candidate >= earliestExecutionTime)
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
