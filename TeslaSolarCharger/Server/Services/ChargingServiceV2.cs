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
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

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
    private readonly IPowerToControlCalculationService _powerToControlCalculationService;
    private readonly IBackendApiService _backendApiService;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly IIssueKeys _issueKeys;
    private readonly INotChargingWithExpectedPowerReasonHelper _notChargingWithExpectedPowerReasonHelper;

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
        IValidFromToSplitter validFromToSplitter,
        IPowerToControlCalculationService powerToControlCalculationService,
        IBackendApiService backendApiService,
        IErrorHandlingService errorHandlingService,
        IIssueKeys issueKeys,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper)
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
        _powerToControlCalculationService = powerToControlCalculationService;
        _backendApiService = backendApiService;
        _errorHandlingService = errorHandlingService;
        _issueKeys = issueKeys;
        _notChargingWithExpectedPowerReasonHelper = notChargingWithExpectedPowerReasonHelper;
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


        var alreadyControlledLoadPoints = new HashSet<(int? carId, int? connectorId)>();
        var maxAdditionalCurrent = _configurationWrapper.MaxCombinedCurrent() - chargingLoadPoints.Select(l => l.ChargingCurrent).Sum();
        foreach (var activeChargingSchedule in activeChargingSchedules)
        {
            if (powerToControl < activeChargingSchedule.OnlyChargeOnAtLeastSolarPower)
            {
                _logger.LogDebug("Skipping charging schedule {@chargingSchedule} as is only placeholder and car should charge with solar power", activeChargingSchedule);
                continue;
            }
            if (powerToControl > activeChargingSchedule.ChargingPower)
            {
                _logger.LogDebug("Skipping charging schedule {@chargingSchedule} as power to control {powerToControl} is higher than charging power {chargingPower}, so ignore charging schedule but charge with solar power.", activeChargingSchedule, powerToControl, activeChargingSchedule.ChargingPower);
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
            _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(activeChargingSchedule.CarId, activeChargingSchedule.OccpChargingConnectorId, new("Charging schedule is active to reach charging target in time"));
            alreadyControlledLoadPoints.Add((activeChargingSchedule.CarId, activeChargingSchedule.OccpChargingConnectorId));
            var powerBeforeChanges = correspondingLoadPoint.ChargingPower;
            var result = await ForceSetLoadPointPower(activeChargingSchedule.CarId, activeChargingSchedule.OccpChargingConnectorId, correspondingLoadPoint, activeChargingSchedule.ChargingPower, maxAdditionalCurrent,
                    cancellationToken).ConfigureAwait(false);
            powerToControl -= powerBeforeChanges;
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
            var powerBeforeChanges = correspondingLoadPoint.ChargingPower;
            var result = await SetLoadPointPower(loadPoint.CarId, loadPoint.ChargingConnectorId, correspondingLoadPoint, powerToControl, maxAdditionalCurrent, cancellationToken).ConfigureAwait(false);
            powerToControl -= powerBeforeChanges;
            powerToControl -= result.powerIncrease;
            maxAdditionalCurrent -= result.currentIncrease;
        }
        _notChargingWithExpectedPowerReasonHelper.UpdateReasonsInSettings();
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
            var minPower = GetPowerAtPhasesAndCurrent(dtoLoadPointOverview.ActualPhases.Value, dtoLoadPointOverview.MinCurrent.Value);
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
                if (car.MinimumSoC > car.SoC || (car.ChargeModeV2 == ChargeModeV2.MaxPower))
                {
                    var energyToCharge = car.ChargeModeV2 == ChargeModeV2.MaxPower
                    ? CalculateEnergyToCharge(
                            car.SocLimit ?? 100,
                            car.SoC ?? 0,
                            carUsableEnergy.Value)
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

                var nextTarget = await GetRelevantTarget(car.Id, currentDate, cancellationToken).ConfigureAwait(false);
                if (nextTarget != default)
                {
                    var energyToCharge = CalculateEnergyToCharge(
                        nextTarget.TargetSoc,
                        car.SoC ?? 0,
                        carUsableEnergy.Value);
                    var maxPower = GetPowerAtPhasesAndCurrent(maxPhases.Value, maxCurrent.Value);
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

    private int GetPowerAtPhasesAndCurrent(int phases, decimal current)
    {
        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        return (int)(phases * current * voltage);
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
                var ampChangeResult = await _ocppChargePointActionService.SetChargingCurrent(chargingConnectorId!.Value, currentToSet, canChangePhases ? phasesToUse : null, cancellationToken).ConfigureAwait(false);
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
                var car = _settings.Cars.First(c => c.Id == loadpoint.CarId!.Value);
                if (car.SoC > (car.SocLimit - _constants.MinimumSocDifference))
                {
                    _logger.LogDebug("Do not start charging for car {carId} as soc is to high compared to soc Limit", loadpoint.CarId);
                    return (0, 0);
                }
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

        _logger.LogTrace("{method} variables: minCurrent={minCurrent}, maxCurrent={maxCurrent}, minPhases={minPhases}, maxPhases={maxPhases}, useCarToManageChargingSpeed={useCarToManageChargingSpeed}, canChangePhases={canChangePhases}, carChargeMode={carChargeMode}, connectorChargeMode={connectorChargeMode}, maxSoc={maxSoc}",
            nameof(SetLoadPointPower), minCurrent, maxCurrent, minPhases, maxPhases, useCarToManageChargingSpeed, canChangePhases, carChargeMode, connectorChargeMode, maxSoc);

        // Decision: minPhases known?
        _logger.LogTrace("{method} evaluating: minPhases = {minPhases}", nameof(SetLoadPointPower), minPhases);
        if (minPhases == default)
        {
            _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new("Min phases is unknown, therefore can not calculate required power to set"));
            _logger.LogTrace("{method} DECISION: minPhases is unknown - returning (0, 0)", nameof(SetLoadPointPower));
            _logger.LogError("Min phases unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }
        _logger.LogTrace("{method} DECISION: minPhases is known ({minPhases}) - continuing", nameof(SetLoadPointPower), minPhases);

        // Decision: maxPhases known?
        _logger.LogTrace("{method} evaluating: maxPhases = {maxPhases}", nameof(SetLoadPointPower), maxPhases);
        if (maxPhases == default)
        {
            _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new("Max phases is unknown, therefore can not calculate required power to set"));
            _logger.LogTrace("{method} DECISION: maxPhases is unknown - returning (0, 0)", nameof(SetLoadPointPower));
            _logger.LogError("Max phases unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }
        _logger.LogTrace("{method} DECISION: maxPhases is known ({maxPhases}) - continuing", nameof(SetLoadPointPower), maxPhases);

        // Decision: minCurrent known?
        _logger.LogTrace("{method} evaluating: minCurrent = {minCurrent}", nameof(SetLoadPointPower), minCurrent);
        if (minCurrent == default)
        {
            _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new("Min current is unknown, therefore can not calculate required power to set"));
            _logger.LogTrace("{method} DECISION: minCurrent is unknown - returning (0, 0)", nameof(SetLoadPointPower));
            _logger.LogError("Min current unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            return (0, 0);
        }
        _logger.LogTrace("{method} DECISION: minCurrent is known ({minCurrent}) - continuing", nameof(SetLoadPointPower), minCurrent);

        // Decision: maxCurrent known?
        _logger.LogTrace("{method} evaluating: maxCurrent = {maxCurrent}", nameof(SetLoadPointPower), maxCurrent);
        if (maxCurrent == default)
        {
            _logger.LogTrace("{method} DECISION: maxCurrent is unknown - returning (0, 0)", nameof(SetLoadPointPower));
            _logger.LogError("Max current unknown for loadpoint {carId}, {connectorId}", carId, chargingConnectorId);
            _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new("Max current is unknown, therefore can not calculate required power to set"));
            return (0, 0);
        }
        _logger.LogTrace("{method} DECISION: maxCurrent is known ({maxCurrent}) - continuing", nameof(SetLoadPointPower), maxCurrent);

        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        var powerBeforeChanges = correspondingLoadPoint.ChargingPower;
        var currentBeforeChanges = correspondingLoadPoint.ChargingCurrent;

        var timeSpanUntilSwitchOn = _configurationWrapper.TimespanUntilSwitchOn();
        var timeSpanUntilSwitchOff = _configurationWrapper.TimespanUntilSwitchOff();

        _logger.LogTrace("{method} variables: voltage={voltage}, powerBeforeChanges={powerBeforeChanges}, currentBeforeChanges={currentBeforeChanges}",
            nameof(SetLoadPointPower), voltage, powerBeforeChanges, currentBeforeChanges);

        // Decision: useCarToManageChargingSpeed AND connector assigned?
        _logger.LogTrace("{method} evaluating: useCarToManageChargingSpeed={useCarToManageChargingSpeed}, chargingConnectorId={connectorId})",
            nameof(SetLoadPointPower), useCarToManageChargingSpeed, chargingConnectorId);
        if (useCarToManageChargingSpeed && chargingConnectorId != default)
        {
            _logger.LogTrace("{method} DECISION: Using car to manage charging speed AND connector assigned - setting OCPP to max power", nameof(SetLoadPointPower));

            #region Set OCPP to max power on OCPP loadpoints where car is directly controlled by TSC

            var anyOpenTransaction = await _context.OcppTransactions
                .Where(t => t.ChargingStationConnectorId == chargingConnectorId
                                        && t.EndDate == default)
                .AnyAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Decision: anyOpenTransaction?
            _logger.LogTrace("{method} evaluating: anyOpenTransaction = {anyOpenTransaction}", nameof(SetLoadPointPower), anyOpenTransaction);
            if (!anyOpenTransaction)
            {
                _logger.LogTrace("{method} DECISION: No open transaction - starting new charge with max power", nameof(SetLoadPointPower));
                var chargeStartResponse = await _ocppChargePointActionService
                    .StartCharging(chargingConnectorId.Value, maxCurrent.Value, maxPhases, cancellationToken)
                    .ConfigureAwait(false);

                // Decision: chargeStartResponse.HasError?
                _logger.LogTrace("{method} evaluating: chargeStartResponse.HasError = {hasError}", nameof(SetLoadPointPower), chargeStartResponse.HasError);
                if (chargeStartResponse.HasError)
                {
                    _logger.LogTrace("{method} DECISION: Charge start failed - returning (0, 0)", nameof(SetLoadPointPower));
                    _logger.LogError("Error start OCPP charge point with max power for connector {loadpointId}: {errorMessage}",
                        chargingConnectorId, chargeStartResponse.ErrorMessage);
                    return (0, 0);
                }
                _logger.LogTrace("{method} DECISION: Charge start successful - continuing", nameof(SetLoadPointPower));
            }
            else
            {
                _logger.LogTrace("{method} DECISION: Open transaction exists - checking if update needed", nameof(SetLoadPointPower));
                if (_settings.OcppConnectorStates.TryGetValue(chargingConnectorId.Value, out var connectorState))
                {
                    // Decision: connectorState.LastSetCurrent vs maxCurrent and connectorState.LastSetPhases vs maxPhases
                    _logger.LogTrace("{method} evaluating: LastSetCurrent={lastSetCurrent}, maxCurrent={maxCurrent}, LastSetPhases={lastSetPhases}, maxPhases={maxPhases}",
                        nameof(SetLoadPointPower),
                        connectorState.LastSetCurrent.Value,
                        maxCurrent,
                        connectorState.LastSetPhases.Value,
                        maxPhases);

                    if ((connectorState.LastSetCurrent.Value != maxCurrent) || (connectorState.LastSetPhases.Value != maxPhases))
                    {
                        _logger.LogTrace("{method} DECISION: Current or phases need update - updating to max power", nameof(SetLoadPointPower));
                        var chargeUpdateResponse = await _ocppChargePointActionService
                            .SetChargingCurrent(chargingConnectorId.Value, maxCurrent.Value, maxPhases, cancellationToken)
                            .ConfigureAwait(false);

                        // Decision: chargeUpdateResponse.HasError?
                        _logger.LogTrace("{method} evaluating: chargeUpdateResponse.HasError = {hasError}", nameof(SetLoadPointPower), chargeUpdateResponse.HasError);
                        if (chargeUpdateResponse.HasError)
                        {
                            _logger.LogTrace("{method} DECISION: Charge update failed - returning (0, 0)", nameof(SetLoadPointPower));
                            _logger.LogError("Error setting OCPP charge point to max power for connector {loadpointId}: {errorMessage}",
                                chargingConnectorId, chargeUpdateResponse.ErrorMessage);
                            return (0, 0);
                        }
                        _logger.LogTrace("{method} DECISION: Charge update successful - continuing", nameof(SetLoadPointPower));
                    }
                    else
                    {
                        _logger.LogTrace("{method} DECISION: Current and phases already at max - no update needed", nameof(SetLoadPointPower));
                    }
                }
            }

            #endregion
        }
        else
        {
            _logger.LogTrace("{method} DECISION: Not using car to manage charging speed OR no connector assigned - skipping OCPP max power setup", nameof(SetLoadPointPower));
        }

        var car = _settings.Cars.FirstOrDefault(c => c.Id == carId);
        DtoOcppConnectorState? ocppConnectorState = null;
        if (chargingConnectorId != default && _settings.OcppConnectorStates.TryGetValue(chargingConnectorId.Value, out var state))
        {
            ocppConnectorState = state;
        }

        _logger.LogTrace("{method} variables: car.State={carState}, car.IsHomeGeofence={isHomeGeofence}, ocppConnectorState.IsCharging={ocppIsCharging}",
            nameof(SetLoadPointPower), car?.State, car?.IsHomeGeofence, ocppConnectorState?.IsCharging.Value);

        bool isCharging;
        // Decision: useCarToManageChargingSpeed branch for isCharging
        _logger.LogTrace("{method} evaluating isCharging: useCarToManageChargingSpeed={useCarToManageChargingSpeed}",
            nameof(SetLoadPointPower), useCarToManageChargingSpeed);

        if (useCarToManageChargingSpeed)
        {
            isCharging = (car!.State == CarStateEnum.Charging) && (car!.IsHomeGeofence == true);
            _logger.LogTrace("{method} DECISION: Using car to determine charging status - isCharging={isCharging} (car.State={carState}, car.IsHomeGeofence={isHomeGeofence})",
                nameof(SetLoadPointPower), isCharging, car.State, car.IsHomeGeofence);
        }
        else
        {
            isCharging = ocppConnectorState?.IsCharging.Value ?? false;
            _logger.LogTrace("{method} DECISION: Using OCPP to determine charging status - isCharging={isCharging}", nameof(SetLoadPointPower), isCharging);
        }

        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        _logger.LogTrace("{method} variables: currentDate={currentDate}", nameof(SetLoadPointPower), currentDate);

        if (isCharging)
        {
            _logger.LogTrace("{method} DECISION: Currently charging - checking if should stop", nameof(SetLoadPointPower));

            #region Stop charging if required

            if (useCarToManageChargingSpeed)
            {
                _logger.LogTrace("{method} evaluating stop conditions (car): carChargeMode={carChargeMode}, car.ShouldStopCharging.Value={shouldStop}, car.ShouldStopCharging.LastChanged={lastChanged}, threshold={threshold}, maxSoc={maxSoc}, car.SoC={carSoc}",
                    nameof(SetLoadPointPower),
                    carChargeMode,
                    car!.ShouldStopCharging.Value,
                    car.ShouldStopCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOff(),
                    maxSoc,
                    car.SoC);

                var isShouldStopChargingRelevant =
                    IsTimeStampedValueRelevant(car.ShouldStopCharging, currentDate, timeSpanUntilSwitchOff, out var shouldStopChargingRelevantAt);
                if ((carChargeMode == ChargeModeV2.Off)
                    || ((carChargeMode == ChargeModeV2.Auto)
                        && ((car.ShouldStopCharging.Value == true) && isShouldStopChargingRelevant)
                    || ((carChargeMode == ChargeModeV2.Auto)
                        && (maxSoc <= car.SoC))))
                {
                    _logger.LogTrace("{method} DECISION: Should stop charging (car) - stopping and returning power decrease", nameof(SetLoadPointPower));
                    // ToDo: add error handling
                    await _teslaService.StopCharging(car.Id).ConfigureAwait(false);
                    return (-powerBeforeChanges, -currentBeforeChanges);
                }
                if ((carChargeMode == ChargeModeV2.Auto))
                {
                    if ((car.ShouldStopCharging.Value == true)
                        && !isShouldStopChargingRelevant)
                    {
                        var reason =
                            new DtoNotChargingWithExpectedPowerReason(
                                $"Waiting {timeSpanUntilSwitchOff} without enough power before charging stops until ",
                                shouldStopChargingRelevantAt);
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, reason);
                    }
                    if (maxSoc <= car.SoC)
                    {
                        var reason = new DtoNotChargingWithExpectedPowerReason("Car reached max SoC.");
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, reason);
                    }
                }
                _logger.LogTrace("{method} DECISION: Should not stop charging (car) - continuing", nameof(SetLoadPointPower));
            }
            else
            {
                _logger.LogTrace(
                    "{method} evaluating wrongPhaseCount (OCPP): phaseCount={phaseCount}, canHandleOnePhase={canHandleOnePhase}, canHandleThreePhase={canHandleThreePhase}, onePhaseLastChanged={onePhaseLastChanged}, threePhaseLastChanged={threePhaseLastChanged}, switchOnThreshold={switchOnThreshold}, switchOffThreshold={switchOffThreshold}",
                    nameof(SetLoadPointPower),
                    ocppConnectorState!.PhaseCount.Value,
                    ocppConnectorState.CanHandlePowerOnOnePhase.Value,
                    ocppConnectorState.CanHandlePowerOnThreePhase.Value,
                    ocppConnectorState.CanHandlePowerOnOnePhase.LastChanged,
                    ocppConnectorState.CanHandlePowerOnThreePhase.LastChanged,
                    currentDate - timeSpanUntilSwitchOn,
                    currentDate - timeSpanUntilSwitchOff);
                var wrongPhaseCount = ((ocppConnectorState!.PhaseCount.Value == 1)
                    && (ocppConnectorState.CanHandlePowerOnOnePhase.Value == false)
                    && IsTimeStampedValueRelevant(ocppConnectorState.CanHandlePowerOnOnePhase, currentDate, timeSpanUntilSwitchOn, out _)
                    && (ocppConnectorState.CanHandlePowerOnThreePhase.Value == true)
                    && IsTimeStampedValueRelevant(ocppConnectorState.CanHandlePowerOnThreePhase, currentDate, timeSpanUntilSwitchOn, out _))
                    || (ocppConnectorState.PhaseCount.Value == 3
                    && (ocppConnectorState.CanHandlePowerOnOnePhase.Value == true)
                    && IsTimeStampedValueRelevant(ocppConnectorState.CanHandlePowerOnOnePhase, currentDate, timeSpanUntilSwitchOff, out _)
                    && (ocppConnectorState.CanHandlePowerOnThreePhase.Value == false)
                    && IsTimeStampedValueRelevant(ocppConnectorState.CanHandlePowerOnThreePhase, currentDate, timeSpanUntilSwitchOff, out _));

                _logger.LogTrace("{method} evaluating stop conditions (OCPP): connectorChargeMode={connectorChargeMode}, ocppConnectorState.ShouldStopCharging.Value={shouldStop}, ocppConnectorState.ShouldStopCharging.LastChanged={lastChanged}, threshold={threshold}, wrongPhaseCount={wrongPhaseCount}",
                    nameof(SetLoadPointPower),
                    connectorChargeMode,
                    ocppConnectorState!.ShouldStopCharging.Value,
                    ocppConnectorState.ShouldStopCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOff(),
                    wrongPhaseCount);

                if ((connectorChargeMode == ChargeModeV2.Off)
                    || ((connectorChargeMode == ChargeModeV2.Auto)
                        && (ocppConnectorState!.ShouldStopCharging.Value == true)
                        && (ocppConnectorState.ShouldStopCharging.LastChanged < (currentDate - _configurationWrapper.TimespanUntilSwitchOff())))
                    || (wrongPhaseCount
                        && (connectorChargeMode == ChargeModeV2.Auto)))
                {
                    _logger.LogTrace("{method} DECISION: Should stop charging (OCPP) - stopping and returning power decrease", nameof(SetLoadPointPower));
                    var result = await _ocppChargePointActionService.StopCharging(chargingConnectorId!.Value, cancellationToken);

                    _logger.LogTrace("{method} evaluating: StopCharging result.HasError = {hasError}", nameof(SetLoadPointPower), result.HasError);
                    if (result.HasError)
                    {
                        _logger.LogTrace("{method} DECISION: Stop charging failed - returning (0, 0)", nameof(SetLoadPointPower));
                        _logger.LogError("Error stopping OCPP charge point for connector {loadpointId}: {errorMessage}",
                            chargingConnectorId, result.ErrorMessage);
                        return (0, 0);
                    }
                    _logger.LogTrace("{method} DECISION: Stop charging successful - returning power decrease", nameof(SetLoadPointPower));
                    return (-powerBeforeChanges, -currentBeforeChanges);
                }
                else if ((connectorChargeMode == ChargeModeV2.Auto)
                         && !wrongPhaseCount
                         && (((ocppConnectorState!.PhaseCount.Value == 1)
                             && (ocppConnectorState.CanHandlePowerOnOnePhase.Value == false)
                             && (ocppConnectorState.CanHandlePowerOnThreePhase.Value == true))
                            || ((ocppConnectorState!.PhaseCount.Value == 3)
                             && (ocppConnectorState.CanHandlePowerOnOnePhase.Value == true)
                             && (ocppConnectorState.CanHandlePowerOnThreePhase.Value == false))))
                {
                    _logger.LogTrace("{method} DECISION: Should switch phases but duration to wait is not over, yet", nameof(SetLoadPointPower));
                    var durationToWait = ocppConnectorState.PhaseCount.Value == 1 ? timeSpanUntilSwitchOn : timeSpanUntilSwitchOff;
                    var startTime = ocppConnectorState.CanHandlePowerOnOnePhase.LastChanged > ocppConnectorState.CanHandlePowerOnThreePhase.LastChanged
                        ? ocppConnectorState.CanHandlePowerOnOnePhase.LastChanged
                        : ocppConnectorState.CanHandlePowerOnThreePhase.LastChanged;
                    var shouldStopChargingRelevantAt = startTime + durationToWait;
                    var reason =
                        new DtoNotChargingWithExpectedPowerReason(
                            $"Waiting {durationToWait} before switching phases until ",
                            shouldStopChargingRelevantAt);
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, reason);
                }
                _logger.LogTrace("{method} DECISION: Should not stop charging (OCPP) - continuing", nameof(SetLoadPointPower));
            }

            #endregion
        }

        if (!isCharging)
        {
            _logger.LogTrace("{method} DECISION: Not currently charging - checking if should start", nameof(SetLoadPointPower));

            #region Start charging if required

            if (useCarToManageChargingSpeed)
            {
                _logger.LogTrace("{method} evaluating start conditions (car): carChargeMode={carChargeMode}, car.ShouldStartCharging.Value={shouldStart}, car.ShouldStartCharging.LastChanged={lastChanged}, threshold={threshold}, car.SocLimit={socLimit}, car.SoC={carSoc}, minSocDifference={minSocDifference}",
                    nameof(SetLoadPointPower),
                    carChargeMode,
                    car!.ShouldStartCharging.Value,
                    car.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn(),
                    car.SocLimit,
                    car.SoC,
                    _constants.MinimumSocDifference);

                var isShouldStartChargingRelevant =
                    IsTimeStampedValueRelevant(car.ShouldStartCharging, currentDate, timeSpanUntilSwitchOn, out var relevantAt);
                if ((carChargeMode == ChargeModeV2.Auto)
                    && (car!.ShouldStartCharging.Value == true)
                    && (isShouldStartChargingRelevant)
                    && (!(car.SoC > (car.SocLimit - _constants.MinimumSocDifference)))
                    && (!(car.SoC >= maxSoc)))
                {
                    _logger.LogTrace("{method} DECISION: Should start charging (car) - calculating current", nameof(SetLoadPointPower));

                    var currentToStartChargingWith = powerToSet / voltage / car.ActualPhases;
                    _logger.LogTrace("{method} variables: initial currentToStartChargingWith={currentToStartChargingWith}, car.ActualPhases={actualPhases}",
                        nameof(SetLoadPointPower), currentToStartChargingWith, car.ActualPhases);

                    if (maxAdditionalCurrent < (currentToStartChargingWith - currentBeforeChanges))
                    {
                        currentToStartChargingWith = (int)(currentBeforeChanges + maxAdditionalCurrent);
                        _logger.LogTrace("{method} DECISION: Limited by maxAdditionalCurrent - adjusted currentToStartChargingWith={currentToStartChargingWith}", nameof(SetLoadPointPower), currentToStartChargingWith);
                    }

                    if (currentToStartChargingWith < minCurrent)
                    {
                        _logger.LogTrace("{method} DECISION: Current too low ({currentToStartChargingWith} < {minCurrent}) - returning (0, 0)", nameof(SetLoadPointPower), currentToStartChargingWith, minCurrent);
                        return (0, 0);
                    }

                    if (currentToStartChargingWith > maxCurrent)
                    {
                        currentToStartChargingWith = maxCurrent.Value;
                        _logger.LogTrace("{method} DECISION: Limited by maxCurrent - adjusted currentToStartChargingWith={currentToStartChargingWith}", nameof(SetLoadPointPower), currentToStartChargingWith);
                    }

                    if (car.SocLimit < (car.SoC + _constants.MinimumSocDifference))
                    {
                        _logger.LogTrace("{method} DECISION: SOC limit too low - returning (0, 0)", nameof(SetLoadPointPower));
                        _logger.LogTrace("Car {carId} has a SOC limit of {socLimit}, which is too low to start charging. Current SOC: {currentSoc}",
                            car.Id, car.SocLimit, car.SoC);
                        return (0, 0);
                    }

                    _logger.LogTrace("{method} DECISION: Starting charging with current={currentToStartChargingWith}", nameof(SetLoadPointPower), currentToStartChargingWith);
                    await _teslaService.StartCharging(car.Id, currentToStartChargingWith).ConfigureAwait(false);
                    var actuallySetPower = GetPowerAtPhasesAndCurrent(car.ActualPhases, currentToStartChargingWith);
                    return (actuallySetPower, currentToStartChargingWith);
                }
                else if ((carChargeMode == ChargeModeV2.Auto))
                {
                    if (car.SoC > (car.SocLimit - _constants.MinimumSocDifference))
                    {
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new($"Car SoC needs to be at least {_constants.MinimumSocDifference}% below car side Soc limit to start charging."));
                        return (0, 0);
                    }
                    else if (car.ShouldStartCharging.Value == true)
                    {
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new($"Waiting {timeSpanUntilSwitchOn} with enough power to start charging until ", relevantAt));
                        return (0, 0);
                    }
                    else if (car.SoC >= maxSoc)
                    {
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new($"Car Soc ({car.SoC}%) reached configured max soc ({maxSoc}%)"));
                        return (0, 0);
                    }
                }
                _logger.LogTrace("{method} DECISION: Should not start charging (car) - conditions not met", nameof(SetLoadPointPower));
            }
            else
            {
                _logger.LogTrace("{method} evaluating start conditions (OCPP): connectorChargeMode={connectorChargeMode}, ocppConnectorState.ShouldStartCharging.Value={shouldStart}, ocppConnectorState.ShouldStartCharging.LastChanged={lastChanged}, threshold={threshold}",
                    nameof(SetLoadPointPower),
                    connectorChargeMode,
                    ocppConnectorState!.ShouldStartCharging.Value,
                    ocppConnectorState.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn());
                var isShouldStartChargingRelevant =
                        IsTimeStampedValueRelevant(ocppConnectorState.ShouldStartCharging, currentDate, timeSpanUntilSwitchOn, out var relevantAt);
                if ((connectorChargeMode == ChargeModeV2.Auto)
                    && (ocppConnectorState!.ShouldStartCharging.Value == true)
                    && isShouldStartChargingRelevant)
                {
                    _logger.LogTrace("{method} DECISION: Should start charging (OCPP) - determining phases", nameof(SetLoadPointPower));

                    int phasesToStartChargingWith;
                    _logger.LogTrace("{method} evaluating phases: minPhases={minPhases}, maxPhases={maxPhases}", nameof(SetLoadPointPower), minPhases, maxPhases);
                    if (minPhases.Value == maxPhases.Value)
                    {
                        phasesToStartChargingWith = minPhases.Value;
                        _logger.LogTrace("{method} DECISION: Min and max phases equal - using {phases} phases", nameof(SetLoadPointPower), phasesToStartChargingWith);
                    }
                    else
                    {
                        _logger.LogTrace("{method} evaluating phase capabilities: CanHandlePowerOnOnePhase={onePhase}, CanHandlePowerOnThreePhase={threePhase}, OnePhaseLastChanged={onePhaseChanged}, ThreePhaseLastChanged={threePhaseChanged}",
                            nameof(SetLoadPointPower),
                            ocppConnectorState.CanHandlePowerOnOnePhase.Value,
                            ocppConnectorState.CanHandlePowerOnThreePhase.Value,
                            ocppConnectorState.CanHandlePowerOnOnePhase.LastChanged,
                            ocppConnectorState.CanHandlePowerOnThreePhase.LastChanged);

                        if ((ocppConnectorState.CanHandlePowerOnOnePhase.Value == true)
                            && (ocppConnectorState.CanHandlePowerOnThreePhase.Value != true))
                        {
                            phasesToStartChargingWith = minPhases.Value;
                            _logger.LogTrace("{method} DECISION: Can only handle one phase - using {phases} phases", nameof(SetLoadPointPower), phasesToStartChargingWith);
                        }
                        else if ((ocppConnectorState.CanHandlePowerOnThreePhase.Value == true)
                                 && (ocppConnectorState.CanHandlePowerOnOnePhase.Value != true))
                        {
                            phasesToStartChargingWith = maxPhases.Value;
                            _logger.LogTrace("{method} DECISION: Can only handle three phases - using {phases} phases", nameof(SetLoadPointPower), phasesToStartChargingWith);
                        }
                        else if ((ocppConnectorState.CanHandlePowerOnThreePhase.Value == true)
                                 && ocppConnectorState.CanHandlePowerOnThreePhase.LastChanged < ocppConnectorState.CanHandlePowerOnOnePhase.LastChanged)
                        {
                            phasesToStartChargingWith = maxPhases.Value;
                            _logger.LogTrace("{method} DECISION: Three phase capability more recent - using {phases} phases", nameof(SetLoadPointPower), phasesToStartChargingWith);
                        }
                        else
                        {
                            phasesToStartChargingWith = minPhases.Value;
                            _logger.LogTrace("{method} DECISION: Default to min phases - using {phases} phases", nameof(SetLoadPointPower), phasesToStartChargingWith);
                        }
                    }

                    var currentToStartChargingWith = powerToSet / voltage / phasesToStartChargingWith;
                    _logger.LogTrace("{method} variables: initial currentToStartChargingWith={currentToStartChargingWith}, phasesToStartChargingWith={phases}",
                        nameof(SetLoadPointPower), currentToStartChargingWith, phasesToStartChargingWith);

                    if (maxAdditionalCurrent < (currentToStartChargingWith - currentBeforeChanges))
                    {
                        currentToStartChargingWith = (int)(currentBeforeChanges + maxAdditionalCurrent);
                        _logger.LogTrace("{method} DECISION: Limited by maxAdditionalCurrent - adjusted currentToStartChargingWith={currentToStartChargingWith}", nameof(SetLoadPointPower), currentToStartChargingWith);
                    }

                    if (currentToStartChargingWith < minCurrent)
                    {
                        _logger.LogTrace("{method} DECISION: Current too low ({currentToStartChargingWith} < {minCurrent}) - returning (0, 0)", nameof(SetLoadPointPower), currentToStartChargingWith, minCurrent);
                        return (0, 0);
                    }

                    if (currentToStartChargingWith > maxCurrent)
                    {
                        currentToStartChargingWith = maxCurrent.Value;
                        _logger.LogTrace("{method} DECISION: Limited by maxCurrent - adjusted currentToStartChargingWith={currentToStartChargingWith}", nameof(SetLoadPointPower), currentToStartChargingWith);
                    }
                    _logger.LogTrace("{method} variables: lastSetCurrent={currentToStartChargingWith}, isCarFullyCharged={isCarFullyCharged}, lastSetCurrentTimeStamp={lastSetCurrentTimeStamp}, isCarFullyChargedTimeStamp={isCarFullyChargedTimeStamp}",
                        nameof(SetLoadPointPower), ocppConnectorState.LastSetCurrent.Value, ocppConnectorState.IsCarFullyCharged.Value, ocppConnectorState.LastSetCurrent.Timestamp, ocppConnectorState.IsCarFullyCharged.Timestamp);
                    if ((ocppConnectorState.LastSetCurrent.Value > 0)
                        && (ocppConnectorState.IsCarFullyCharged.Value == true)
                        && (ocppConnectorState.LastSetCurrent.Timestamp > ocppConnectorState.IsCarFullyCharged.Timestamp))
                    {
                        _logger.LogTrace("{method} DECISION: Car is fully charged and last set current > 0 - returning (0, 0)", nameof(SetLoadPointPower));
                        _logger.LogTrace("Do not try to start charging as last set Current is greater than 0 and car is fully charged");
                        return (0, 0);
                    }

                    if (canChangePhases
                        && (ocppConnectorState.LastSetPhases.Value != default)
                        && (phasesToStartChargingWith != ocppConnectorState.LastSetPhases.Value))
                    {
                        _logger.LogTrace("{method} DECISION: Phase switching conditions met - checking cooldown", nameof(SetLoadPointPower));

                        _logger.LogTrace("{method} Should start charging with {newPhaseCount} phases while LastSetPhases is {oldPhaseCount} phases before charge stop",
                            nameof(SetLoadPointPower), phasesToStartChargingWith, ocppConnectorState.LastSetPhases.Value);

                        var phaseSwitchCoolDownTimeSeconds = await _context.OcppChargingStationConnectors
                            .Where(c => c.Id == chargingConnectorId!.Value)
                            .Select(c => c.PhaseSwitchCoolDownTimeSeconds)
                            .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                        _logger.LogTrace("{method} Retrieved cooldown configuration: PhaseSwitchCoolDownTimeSeconds={cooldownSeconds}",
                            nameof(SetLoadPointPower), phaseSwitchCoolDownTimeSeconds);

                        if (phaseSwitchCoolDownTimeSeconds != default)
                        {
                            var phaseSwitchCoolDownTime = TimeSpan.FromSeconds(phaseSwitchCoolDownTimeSeconds.Value);
                            var timeSinceChargeStop = currentDate - ocppConnectorState.IsCharging.LastChanged;

                            _logger.LogTrace("{method} Cooldown evaluation: phaseSwitchCoolDownTime={cooldownTime}, timeSinceChargeStop={timeSinceStop}, currentDate={currentDate}",
                                nameof(SetLoadPointPower), phaseSwitchCoolDownTime, timeSinceChargeStop, currentDate);

                            if (phaseSwitchCoolDownTime > timeSinceChargeStop)
                            {
                                _logger.LogTrace("{method} DECISION: Not starting charging - cooldown time of {phaseSwitchCoolDownTime} is not over since charging stopped at {chargeStop}",
                                    nameof(SetLoadPointPower), phaseSwitchCoolDownTime, ocppConnectorState.IsCharging.LastChanged);
                                return (0, 0);
                            }
                            else
                            {
                                _logger.LogTrace("{method} DECISION: Cooldown period has passed - proceeding with phase switching",
                                    nameof(SetLoadPointPower));
                            }
                        }
                        else
                        {
                            _logger.LogTrace("{method} DECISION: No cooldown configured - proceeding with phase switching",
                                nameof(SetLoadPointPower));
                        }
                    }
                    else
                    {
                        _logger.LogTrace("{method} DECISION: Phase switching conditions not met - no action needed",
                            nameof(SetLoadPointPower));
                    }

                    _logger.LogTrace("{method} DECISION: Starting OCPP charging with current={currentToStartChargingWith}, phases={phases}", nameof(SetLoadPointPower), currentToStartChargingWith, phasesToStartChargingWith);
                    var result = await _ocppChargePointActionService.StartCharging(chargingConnectorId!.Value, currentToStartChargingWith, canChangePhases ? phasesToStartChargingWith : null, cancellationToken).ConfigureAwait(false);

                    _logger.LogTrace("{method} evaluating: StartCharging result.HasError = {hasError}", nameof(SetLoadPointPower), result.HasError);
                    if (result.HasError)
                    {
                        _logger.LogTrace("{method} DECISION: Start charging failed - returning (0, 0)", nameof(SetLoadPointPower));
                        _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                            chargingConnectorId, result.ErrorMessage);
                        return (0, 0);
                    }

                    _logger.LogTrace("{method} DECISION: Start charging successful - returning power increase", nameof(SetLoadPointPower));
                    var actuallySetPower = GetPowerAtPhasesAndCurrent(phasesToStartChargingWith, currentToStartChargingWith);
                    return (actuallySetPower, currentToStartChargingWith);
                }
                else if ((connectorChargeMode == ChargeModeV2.Auto)
                         && (ocppConnectorState!.ShouldStartCharging.Value == true))
                {
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new($"Waiting {timeSpanUntilSwitchOn} with enough power to start charging until ", relevantAt));
                }
                _logger.LogTrace("{method} DECISION: Should not start charging (OCPP) - conditions not met", nameof(SetLoadPointPower));
            }

            #endregion
        }

        if (!isCharging)
        {
            _logger.LogTrace("{method} DECISION: Not charging - checking if should remain stopped", nameof(SetLoadPointPower));

            #region Let charging stopped if should not start

            if (useCarToManageChargingSpeed)
            {
                _logger.LogTrace("{method} evaluating stop conditions (car): carChargeMode={carChargeMode}, car.ShouldStartCharging.Value={shouldStart}, car.ShouldStartCharging.LastChanged={lastChanged}, threshold={threshold}, maxSoc={maxSoc}, car.SoC={carSoc}",
                    nameof(SetLoadPointPower),
                    carChargeMode,
                    car!.ShouldStartCharging.Value,
                    car.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn(),
                    maxSoc,
                    car.SoC);

                if ((carChargeMode == ChargeModeV2.Manual)
                    || (carChargeMode == ChargeModeV2.Off)
                    || (car.ShouldStartCharging.Value == false)
                    || (car.ShouldStartCharging.LastChanged > (currentDate - _configurationWrapper.TimespanUntilSwitchOn()))
                    || (maxSoc <= car.SoC))
                {
                    _logger.LogTrace("{method} DECISION: Should remain stopped (car) - returning (0, 0)", nameof(SetLoadPointPower));
                    return (0, 0);
                }
            }
            else
            {
                _logger.LogTrace("{method} evaluating stop conditions (OCPP): connectorChargeMode={connectorChargeMode}, ocppConnectorState.ShouldStartCharging.Value={shouldStart}, ocppConnectorState.ShouldStartCharging.LastChanged={lastChanged}, threshold={threshold}",
                    nameof(SetLoadPointPower),
                    connectorChargeMode,
                    ocppConnectorState!.ShouldStartCharging.Value,
                    ocppConnectorState.ShouldStartCharging.LastChanged,
                    currentDate - _configurationWrapper.TimespanUntilSwitchOn());

                if ((connectorChargeMode == ChargeModeV2.Manual)
                    || (connectorChargeMode == ChargeModeV2.Off)
                    || (ocppConnectorState.ShouldStartCharging.Value == false)
                    || (ocppConnectorState.ShouldStartCharging.LastChanged > (currentDate - _configurationWrapper.TimespanUntilSwitchOn())))
                {
                    _logger.LogTrace("{method} DECISION: Should remain stopped (OCPP) - returning (0, 0)", nameof(SetLoadPointPower));
                    return (0, 0);
                }
            }

            #endregion
        }

        if (isCharging)
        {
            _logger.LogTrace("{method} DECISION: Currently charging - adjusting power/current", nameof(SetLoadPointPower));

            #region Continue to charge with new values

            if (useCarToManageChargingSpeed)
            {
                _logger.LogTrace("{method} evaluating charging adjustment (car): carChargeMode={carChargeMode}", nameof(SetLoadPointPower), carChargeMode);
                if (carChargeMode == ChargeModeV2.Auto)
                {
                    _logger.LogTrace("{method} DECISION: Car in Auto mode - calculating new current", nameof(SetLoadPointPower));

                    var currentToChargeWith = powerToSet / voltage / car!.ActualPhases;
                    _logger.LogTrace("{method} variables: initial currentToChargeWith={currentToChargeWith}, car.ActualPhases={actualPhases}",
                        nameof(SetLoadPointPower), currentToChargeWith, car.ActualPhases);

                    if (maxAdditionalCurrent < (currentToChargeWith - currentBeforeChanges))
                    {
                        currentToChargeWith = (int)(currentBeforeChanges + maxAdditionalCurrent);
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new("Charge speed limited due to Max combined current configured in Base Configuration"));
                        _logger.LogTrace("{method} DECISION: Limited by maxAdditionalCurrent - adjusted currentToChargeWith={currentToChargeWith}", nameof(SetLoadPointPower), currentToChargeWith);
                    }

                    if (currentToChargeWith < minCurrent)
                    {
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new($"The configured minimum current is {minCurrent}A, therefore charge speed is higher than it should be."));
                        currentToChargeWith = minCurrent.Value;
                        _logger.LogTrace("{method} DECISION: Below minCurrent - adjusted to minCurrent={currentToChargeWith}", nameof(SetLoadPointPower), currentToChargeWith);
                    }

                    if (currentToChargeWith > maxCurrent)
                    {
                        currentToChargeWith = maxCurrent.Value;
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new($"Can not further increase charging speed as configured max current is {maxCurrent}A"));
                        _logger.LogTrace("{method} DECISION: Above maxCurrent - adjusted to maxCurrent={currentToChargeWith}", nameof(SetLoadPointPower), currentToChargeWith);
                    }

                    _logger.LogTrace("{method} DECISION: Setting car amp to {currentToChargeWith}", nameof(SetLoadPointPower), currentToChargeWith);
                    await _teslaService.SetAmp(car.Id, currentToChargeWith).ConfigureAwait(false);
                    var actuallySetPower = GetPowerAtPhasesAndCurrent(car.ActualPhases, currentToChargeWith);
                    return (actuallySetPower - powerBeforeChanges, currentToChargeWith - currentBeforeChanges);
                }
                else
                {
                    _logger.LogTrace("{method} DECISION: Car not in Auto mode - returning (0, 0)", nameof(SetLoadPointPower));
                    return (0, 0);
                }
            }
            else
            {
                _logger.LogTrace("{method} evaluating charging adjustment (OCPP): connectorChargeMode={connectorChargeMode}", nameof(SetLoadPointPower), connectorChargeMode);
                if (connectorChargeMode == ChargeModeV2.Auto)
                {
                    _logger.LogTrace("{method} DECISION: OCPP in Auto mode - calculating new current and phases", nameof(SetLoadPointPower));

                    var phasesToChargeWith = ocppConnectorState!.PhaseCount.Value;
                    _logger.LogTrace("{method} variables: initial phasesToChargeWith={phasesToChargeWith}, LastSetPhases={lastSetPhases}",
                        nameof(SetLoadPointPower), phasesToChargeWith, ocppConnectorState.LastSetPhases.Value);

                    if (phasesToChargeWith == default)
                    {
                        if (ocppConnectorState!.LastSetPhases.Value == default)
                        {
                            phasesToChargeWith = maxPhases.Value;
                            _logger.LogTrace("{method} DECISION: No phase info - using maxPhases={phasesToChargeWith}", nameof(SetLoadPointPower), phasesToChargeWith);
                        }
                        else
                        {
                            phasesToChargeWith = ocppConnectorState!.LastSetPhases.Value.Value;
                            _logger.LogTrace("{method} DECISION: Using LastSetPhases={phasesToChargeWith}", nameof(SetLoadPointPower), phasesToChargeWith);
                        }
                    }

                    var currentToChargeWith = (decimal)powerToSet / voltage / phasesToChargeWith.Value;
                    _logger.LogTrace("{method} variables: initial currentToChargeWith={currentToChargeWith}, phasesToChargeWith={phasesToChargeWith}",
                        nameof(SetLoadPointPower), currentToChargeWith, phasesToChargeWith);

                    if (maxAdditionalCurrent < (currentToChargeWith - currentBeforeChanges))
                    {
                        currentToChargeWith = currentBeforeChanges + maxAdditionalCurrent;
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new("Charge speed limited due to Max combined current configured in Base Configuration"));
                        _logger.LogTrace("{method} DECISION: Limited by maxAdditionalCurrent - adjusted currentToChargeWith={currentToChargeWith}", nameof(SetLoadPointPower), currentToChargeWith);
                    }

                    if (currentToChargeWith < minCurrent)
                    {
                        currentToChargeWith = minCurrent.Value;
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new($"The configured minimum current is {minCurrent}A, therefore charge speed is higher than it should be."));
                        _logger.LogTrace("{method} DECISION: Below minCurrent - adjusted to minCurrent={currentToChargeWith}", nameof(SetLoadPointPower), currentToChargeWith);
                    }

                    if (currentToChargeWith > maxCurrent)
                    {
                        currentToChargeWith = maxCurrent.Value;
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(carId, chargingConnectorId, new($"Can not further increase charging speed as configured max current is {maxCurrent}A"));
                        _logger.LogTrace("{method} DECISION: Above maxCurrent - adjusted to maxCurrent={currentToChargeWith}", nameof(SetLoadPointPower), currentToChargeWith);
                    }

                    _logger.LogTrace("{method} DECISION: Setting OCPP charging current to {currentToChargeWith} with {phasesToChargeWith} phases", nameof(SetLoadPointPower), currentToChargeWith, phasesToChargeWith);
                    var ampChangeResult = await _ocppChargePointActionService.SetChargingCurrent(chargingConnectorId!.Value, currentToChargeWith, canChangePhases ? phasesToChargeWith : null, cancellationToken).ConfigureAwait(false);

                    _logger.LogTrace("{method} evaluating: ampChangeResult.HasError = {hasError}", nameof(SetLoadPointPower), ampChangeResult.HasError);
                    if (ampChangeResult.HasError)
                    {
                        _logger.LogTrace("{method} DECISION: Amp change failed - returning (0, 0)", nameof(SetLoadPointPower));
                        _logger.LogError("Error starting OCPP charge point for connector {loadpointId}: {errorMessage}",
                            chargingConnectorId, ampChangeResult.ErrorMessage);
                        return (0, 0);
                    }

                    _logger.LogTrace("{method} DECISION: Amp change successful - returning power change", nameof(SetLoadPointPower));
                    var actuallySetPower = GetPowerAtPhasesAndCurrent(phasesToChargeWith.Value, currentToChargeWith);
                    return (powerBeforeChanges - actuallySetPower, currentBeforeChanges - currentToChargeWith);
                }
                _logger.LogTrace("{method} DECISION: OCPP not in Auto mode - returning (0, 0)", nameof(SetLoadPointPower));
                return (0, 0);
            }

            #endregion
        }

        _logger.LogTrace("{method} DECISION: No path met all conditions - returning (0, 0)", nameof(SetLoadPointPower));
        _logger.LogError("No path meets all conditions, check data why not: {carID}, {connectorId}", carId, chargingConnectorId);
        return (0, 0);
    }

    private bool IsTimeStampedValueRelevant<T>(DtoTimeStampedValue<T> timeStampedValue, DateTimeOffset currentDate,
        TimeSpan timeSpanUntilIsRelevant, out DateTimeOffset? relevantAt)
    {
        relevantAt = null;
        if (timeStampedValue.LastChanged == default)
        {
            return true; // If no last changed time is set, we assume it is relevant as it might never change when the value is true since startup
        }
        var isRelevant = timeStampedValue.LastChanged < (currentDate - timeSpanUntilIsRelevant);
        if (!isRelevant)
        {
            relevantAt = timeStampedValue.LastChanged.Value.Add(timeSpanUntilIsRelevant);
        }
        return isRelevant;
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
                .Select(c => new
                {
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
