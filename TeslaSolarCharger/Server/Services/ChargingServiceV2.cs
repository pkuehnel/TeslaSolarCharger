using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

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
    private readonly ITeslaService _teslaService;
    private readonly IEnergyDataService _energyDataService;
    private readonly IPowerToControlCalculationService _powerToControlCalculationService;
    private readonly IBackendApiService _backendApiService;
    private readonly INotChargingWithExpectedPowerReasonHelper _notChargingWithExpectedPowerReasonHelper;
    private readonly ITargetChargingValueCalculationService _targetChargingValueCalculationService;
    private readonly IAppStateNotifier _appStateNotifier;
    private readonly IChargingScheduleService _chargingScheduleService;
    private readonly IConstants _constants;

    public ChargingServiceV2(ILogger<ChargingServiceV2> logger,
        IConfigurationWrapper configurationWrapper,
        ILoadPointManagementService loadPointManagementService,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        IOcppChargePointActionService ocppChargePointActionService,
        ISettings settings,
        ITeslaService teslaService,
        IEnergyDataService energyDataService,
        IPowerToControlCalculationService powerToControlCalculationService,
        IBackendApiService backendApiService,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper,
        ITargetChargingValueCalculationService targetChargingValueCalculationService,
        IAppStateNotifier appStateNotifier,
        IChargingScheduleService chargingScheduleService,
        IConstants constants)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _loadPointManagementService = loadPointManagementService;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _ocppChargePointActionService = ocppChargePointActionService;
        _settings = settings;
        _teslaService = teslaService;
        _energyDataService = energyDataService;
        _powerToControlCalculationService = powerToControlCalculationService;
        _backendApiService = backendApiService;
        _notChargingWithExpectedPowerReasonHelper = notChargingWithExpectedPowerReasonHelper;
        _targetChargingValueCalculationService = targetChargingValueCalculationService;
        _appStateNotifier = appStateNotifier;
        _chargingScheduleService = chargingScheduleService;
        _constants = constants;
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
        SetManualCarsToAtHome(currentDate);
        await CalculateGeofences(currentDate);
        SetManualCarsToAtHome(currentDate);
        await AddNoOcppConnectionReason(cancellationToken).ConfigureAwait(false);
        await SetCurrentOfNonChargingTeslasToMax().ConfigureAwait(false);
        await SetCarChargingTargetsToFulFilled(currentDate).ConfigureAwait(false);
        var loadPointsToManage = await _loadPointManagementService.GetLoadPointsToManage().ConfigureAwait(false);
        var chargingLoadPoints = await _loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var powerToControl = _powerToControlCalculationService.CalculatePowerToControl(chargingLoadPoints);
        if (ShouldSkipPowerUpdatesDueToTooRecentAmpChangesOrPlugin(chargingLoadPoints, currentDate))
        {
            return;
        }

        AddNotChargingReasons();
        //reduce current for the remaining loadpoints as this loadpoint might start charging with max current
        foreach (var loadpoint in loadPointsToManage)
        {
            if (loadpoint.ChargingConnectorId == default)
            {
                continue;
            }
            var stateAvailable = _settings.OcppConnectorStates.TryGetValue(loadpoint.ChargingConnectorId.Value, out _);
            if (!stateAvailable)
            {
                continue;
            }
        }

        var chargingSchedules = await GenerateChargingSchedules(currentDate, loadPointsToManage, cancellationToken).ConfigureAwait(false);

        _settings.ChargingSchedules = new(chargingSchedules);
        var chargingScheduleChange = new StateUpdateDto
        {
            DataType = DataTypeConstants.ChargingSchedulesChangeTrigger,
            Timestamp = _dateTimeProvider.DateTimeOffSetUtcNow(),
        };
        await _appStateNotifier.NotifyStateUpdateAsync(chargingScheduleChange).ConfigureAwait(false);

        _logger.LogDebug("Final calculated power to control: {powerToControl}", powerToControl);
        var activeChargingSchedules = chargingSchedules.Where(s => s.ValidFrom <= currentDate).ToList();

        var targetChargingValues = loadPointsToManage
            .OrderBy(l => l.ChargingPriority)
            .Select(l => new DtoTargetChargingValues(l))
            .ToList();

        await _targetChargingValueCalculationService.AppendTargetValues(targetChargingValues, activeChargingSchedules, currentDate, powerToControl, 0, cancellationToken);
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

    internal void SetManualCarsToAtHome(DateTimeOffset currentDate)
    {
        var manualCars = _context.Cars.Where(c => c.CarType == CarType.Manual).ToList();
        foreach (var manualCar in manualCars)
        {
            var dtoCar = _settings.Cars.FirstOrDefault(c => c.Id == manualCar.Id);
            if (dtoCar == default)
            {
                continue;
            }
            dtoCar.IsHomeGeofence.Update(currentDate, true);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="targetChargingValue"></param>
    /// <param name="currentDate"></param>
    /// <returns>Succeeded</returns>
    internal async Task<bool> SetChargingPowerOfOccpConnectorForCarManagedLoadpoint(DtoTargetChargingValues targetChargingValue, DateTimeOffset currentDate, CancellationToken cancellationToken)
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

    internal async Task<bool> SetChargingConnectorToMaxPowerAndMaxPhases(int chargingConnectorId,
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
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(null, connectorId,
                    new NotChargingWithExpectedPowerReasonTemplate("OCPP connection not established. After a TSC or charger reboot it can take up to 5 minutes until the charger is connected again."));
            }
        }
    }

    private void AddNotChargingReasons()
    {
        foreach (var dtoCar in _settings.CarsToManage)
        {
            if (dtoCar.IsHomeGeofence.Value != true)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(dtoCar.Id, null,
                    new NotChargingWithExpectedPowerReasonTemplate("Car is not at home"));
            }

            if (dtoCar.PluggedIn.Value != true)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(dtoCar.Id, null,
                    new NotChargingWithExpectedPowerReasonTemplate("Car is not plugged in"));
            }
        }

        foreach (var settingsOcppConnectorState in _settings.OcppConnectorStates)
        {
            if (!settingsOcppConnectorState.Value.IsPluggedIn.Value)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(null, settingsOcppConnectorState.Key,
                    new NotChargingWithExpectedPowerReasonTemplate("Charging connector is not plugged in"));
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
            if (chargingTarget.TargetSoc == default && !chargingTarget.DischargeHomeBatteryToMinSoc)
            {
                _logger.LogError("Chargingtarget {@target} is invalid, delete it", chargingTarget);
                _context.CarChargingTargets.Remove(chargingTarget);
                continue;
            }
            var car = _settings.Cars.First(c => c.Id == chargingTarget.CarId);
            var actualTargetSoc = _chargingScheduleService.GetActualTargetSoc(car.SocLimit.Value, chargingTarget.TargetSoc, car.IsCharging.Value == true);
            if (car.SoC.Value >= actualTargetSoc || car.PluggedIn.Value != true || car.IsHomeGeofence.Value != true)
            {
                chargingTarget.LastFulFilled = currentDate;
            }

            if (actualTargetSoc == default && chargingTarget.DischargeHomeBatteryToMinSoc)
            {
                var homeBatteryTargetSoc = _configurationWrapper.HomeBatteryMinSoc();
                if (_settings.HomeBatterySoc <= homeBatteryTargetSoc)
                {
                    chargingTarget.LastFulFilled = currentDate;
                }
            }

            if (!(chargingTarget.RepeatOnMondays
                || chargingTarget.RepeatOnTuesdays
                || chargingTarget.RepeatOnWednesdays
                || chargingTarget.RepeatOnThursdays
                || chargingTarget.RepeatOnFridays
                || chargingTarget.RepeatOnSaturdays
                || chargingTarget.RepeatOnSundays))
            {
                if (chargingTarget.TargetDate == default)
                {
                    _context.CarChargingTargets.Remove(chargingTarget);
                }
                else
                {
                    var tz = string.IsNullOrWhiteSpace(chargingTarget.ClientTimeZone)
                        ? TimeZoneInfo.Utc
                        : TimeZoneInfo.FindSystemTimeZoneById(chargingTarget.ClientTimeZone);
                    var targetDate = new DateTimeOffset(chargingTarget.TargetDate.Value, chargingTarget.TargetTime,
                        tz.GetUtcOffset(new(chargingTarget.TargetDate.Value, chargingTarget.TargetTime)));
                    if (targetDate < chargingTarget.LastFulFilled)
                    {
                        _context.CarChargingTargets.Remove(chargingTarget);
                    }
                }

            }
        }
        await _context.SaveChangesAsync().ConfigureAwait(false);
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

    private async Task<List<DtoChargingSchedule>> GenerateChargingSchedules(DateTimeOffset currentDate, List<DtoLoadPointOverview> loadPointsToManage,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({currentDate}, {loadPointsToManage})", nameof(GenerateChargingSchedules), currentDate, loadPointsToManage.Count);
        var chargingSchedules = new List<DtoChargingSchedule>();
        var carIds = loadPointsToManage.Where(l => l.CarId != default)
            .Select(l => l.CarId!.Value)
            .ToHashSet();
        var nextTargets = await GetRelevantTargets(carIds, currentDate, cancellationToken).ConfigureAwait(false);
        var predictedSurplusSlices = new Dictionary<DateTimeOffset, int>();
        if (nextTargets.Any())
        {
            var lastTarget = nextTargets.Last();
            var currentFullHour = new DateTimeOffset(currentDate.Year, currentDate.Month, currentDate.Day, currentDate.Hour, 0, 0, currentDate.Offset);
            var fullHourAfterNextTarget = lastTarget.NextExecutionTime.NextFullHour().AddHours(_constants.SolarPowerSurplusPredictionIntervalHours);
            predictedSurplusSlices = await _energyDataService
                .GetPredictedSurplusPerSlice(currentFullHour, fullHourAfterNextTarget, TimeSpan.FromHours(_constants.SolarPowerSurplusPredictionIntervalHours), cancellationToken)
                .ConfigureAwait(false);
        }
        
        foreach (var loadPoint in loadPointsToManage)
        {
            var loadPointRelevantChargingTargets = loadPoint.CarId == default
                ? new()
                : nextTargets.Where(t => t.CarId == loadPoint.CarId.Value)
                    .OrderBy(t => t.NextExecutionTime)
                    .ToList();
            var loadPointChargingTargets = await _chargingScheduleService
                .GenerateChargingSchedulesForLoadPoint(loadPoint, loadPointRelevantChargingTargets, predictedSurplusSlices, currentDate, cancellationToken);
            chargingSchedules.AddRange(loadPointChargingTargets);
        }
        return chargingSchedules.OrderBy(c => c.ValidFrom).ToList();
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

    private async Task<List<DtoTimeZonedChargingTarget>> GetRelevantTargets(IEnumerable<int> carIds, DateTimeOffset currentDate,
    CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({@carIds}, {currentDate})", nameof(GetRelevantTargets), carIds, currentDate);
        var result = new List<DtoTimeZonedChargingTarget>();
        foreach (var carId in carIds)
        {
            var car = _settings.Cars.First(c => c.Id == carId);
            var unfulFilledChargingTargets = await _context.CarChargingTargets
                .Where(c => c.CarId == carId
                            && (!(c.LastFulFilled >= currentDate)))
                .AsNoTracking()
                .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (unfulFilledChargingTargets.Count < 1)
            {
                _logger.LogDebug("No charging targets found for car {carId}.", carId);
                continue;
            }
            var lastPluggedIn = car.PluggedIn.Value == true ? (car.PluggedIn.LastChanged ?? currentDate) : currentDate;
            foreach (var carChargingTarget in unfulFilledChargingTargets)
            {
                var nextTargetUtc = GetNextTargetUtc(carChargingTarget, lastPluggedIn);
                if (nextTargetUtc != default)
                {
                    result.Add(new()
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
                        DischargeHomeBatteryToMinSoc = carChargingTarget.DischargeHomeBatteryToMinSoc,
                    });
                }
            }

        }
        return result
            .OrderBy(t => t.NextExecutionTime)
            .ToList();
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
                    _logger.LogTrace("No repetition set, so return candidate: {candidate} for target {targetId} in timezone {tz}.", candidate, chargingTarget.Id, tz.Id);
                    return candidate.Value.ToUniversalTime();
                }
                // if repetition is set the set date is considered as the earliest execution time. But we still need to check if it is the first enabled weekday
                earliestExecutionTime = candidate.Value;
            }
            // otherwise fall back to the repeating schedule
        }


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
                _logger.LogTrace("Candidate: {candidate} for target {targetId} in timezone {tz}.", candidate, chargingTarget.Id, tz.Id);
                return candidate.Value.ToUniversalTime();
            }
        }

        return null;
    }








}
