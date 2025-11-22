using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class ChargingScheduleService : IChargingScheduleService
{
    private readonly ILogger<ChargingScheduleService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IValidFromToSplitter _validFromToSplitter;
    private readonly IConstants _constants;

    public ChargingScheduleService(ILogger<ChargingScheduleService> logger, ITeslaSolarChargerContext context,
        ISettings settings, IConfigurationWrapper configurationWrapper, IValidFromToSplitter validFromToSplitter,
        IConstants constants)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _validFromToSplitter = validFromToSplitter;
        _constants = constants;
    }

    public async Task<List<DtoChargingSchedule>> GenerateChargingSchedulesForLoadPoint(DtoLoadPointOverview loadpoint,
        List<DtoTimeZonedChargingTarget> nextTargets, Dictionary<DateTimeOffset, int> predictedSurplusSlices, DateTimeOffset currentDate,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({@loadpoint}, {currentDate})", nameof(GenerateChargingSchedulesForLoadPoint), loadpoint, currentDate);
        var schedules = new List<DtoChargingSchedule>();
        if (loadpoint.CarId == default)
        {
            return schedules;
        }
        var car = _settings.Cars.First(c => c.Id == loadpoint.CarId.Value);
        if (car.ChargeModeV2 != ChargeModeV2.Auto)
        {
            return schedules;
        }
        var (carUsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent) = await GetChargingScheduleRelevantData(loadpoint.CarId, loadpoint.ChargingConnectorId).ConfigureAwait(false);
        if (maxPhases == default || maxCurrent == default)
        {
            _logger.LogWarning("Can not schedule charging as at least one required value is unknown.");
            return schedules;
        }

        foreach (var nextTarget in nextTargets)
        {
            _logger.LogTrace("Handle target {@target}", nextTarget);
            if (nextTarget.TargetSoc != default && (carUsableEnergy == default || carSoC == default))
            {
                _logger.LogWarning("Can not handle {@target} as car Soc or carUsableEnergy is unknown", nextTarget);
                continue;
            }
            var minimumEnergyToCharge = GetMinimumEnergyToCharge(currentDate, nextTarget, car, carUsableEnergy, schedules);
            var homeBatteryEnergyToCharge = nextTarget.DischargeHomeBatteryToMinSoc ? CalculateHomeBatteryEnergyToMinSoc() : 0;
            if (minimumEnergyToCharge == 0 && homeBatteryEnergyToCharge == 0)
            {
                _logger.LogDebug("No energy to charge calculated for car {carId}. Do not plan charging schedule.", car.Id);
                continue;
            }

            var maxPower = GetPowerAtPhasesAndCurrent(maxPhases.Value, maxCurrent.Value, loadpoint.EstimatedVoltageWhileCharging);
            //Target was not reached in time so schedule full power until target is reached
            if (nextTarget.NextExecutionTime < currentDate)
            {
                _logger.LogDebug("Next target {@nextTarget} is in the past. Plan charging immediatly.", nextTarget);
                var startDate = currentDate;
                while (minimumEnergyToCharge > 0)
                {
                    var chargingDuration = CalculateChargingDuration(minimumEnergyToCharge, maxPower);
                    var validToDate = startDate + chargingDuration;
                    var chargingScheduleToAdd = new DtoChargingSchedule(loadpoint.CarId, loadpoint.ChargingConnectorId, maxPower)
                    {
                        ValidFrom = startDate,
                        ValidTo = validToDate,
                    };
                    (schedules, var additionalScheduledEnergy) = AddChargingSchedule(schedules, chargingScheduleToAdd, maxPower);
                    minimumEnergyToCharge -= additionalScheduledEnergy;
                    startDate = new(validToDate.Year, validToDate.Month, validToDate.Day, validToDate.Hour,
                        validToDate.Minute, validToDate.Second, validToDate.Offset);
                }
            }

            if (_configurationWrapper.UsePredictedSolarPowerGenerationForChargingSchedules())
            {
                if (minPhases == default || minCurrent == default)
                {
                    _logger.LogWarning("Can not schedule based on solar predictions as minPhases or minCurrent is not set");
                }
                else
                {
                    var minPower = GetPowerAtPhasesAndCurrent(minPhases.Value, minCurrent.Value, loadpoint.EstimatedVoltageWhileCharging);
                    var maxPowerCappedPredictedHoursWithAtLeastMinPowerSurpluses = predictedSurplusSlices
                        .Where(s => s.Value > minPower && s.Key < nextTarget.NextExecutionTime)
                        .OrderBy(s => s.Key)
                        .ToDictionary(s => s.Key, s => s.Value > maxPower ? maxPower : s.Value);
                    foreach (var maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus in maxPowerCappedPredictedHoursWithAtLeastMinPowerSurpluses)
                    {
                        var startDate = maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key < currentDate
                            ? currentDate
                            : maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key;
                        var endDate =
                            maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key.AddHours(_constants.SolarPowerSurplusPredictionIntervalHours) > nextTarget.NextExecutionTime
                                ? nextTarget.NextExecutionTime
                                : maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key.AddHours(_constants.SolarPowerSurplusPredictionIntervalHours);
                        var chargingScheduleToAdd = new DtoChargingSchedule(loadpoint.CarId.Value, loadpoint.ChargingConnectorId, maxPower)
                        {
                            ValidFrom = startDate,
                            ValidTo = endDate,
                            TargetMinPower = 0,
                            EstimatedSolarPower = maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Value,
                        };
                        (schedules, var additionalScheduledEnergy) = AddChargingSchedule(schedules, chargingScheduleToAdd, maxPower);
                        minimumEnergyToCharge -= additionalScheduledEnergy;
                        if (minimumEnergyToCharge <= 0)
                        {
                            var tooMuchChargedEnergy = -minimumEnergyToCharge;
                            if (tooMuchChargedEnergy > 0)
                            {
                                var solarPowerOnlyChargingSchedules = schedules
                                    .Where(s => s.TargetMinPower == 0)
                                    .OrderByDescending(s => s.ValidTo)
                                    .ToList();
                                var i = 0;
                                while (tooMuchChargedEnergy > 0 && i < solarPowerOnlyChargingSchedules.Count)
                                {
                                    var chargingScheduleForThisHour = solarPowerOnlyChargingSchedules[i];
                                    var timeToReduce = TimeSpan.FromHours((double)tooMuchChargedEnergy / chargingScheduleForThisHour.EstimatedSolarPower);
                                    var scheduleDuration = chargingScheduleForThisHour.ValidTo - chargingScheduleForThisHour.ValidFrom;
                                    if (timeToReduce > scheduleDuration)
                                    {
                                        tooMuchChargedEnergy -= (int)(scheduleDuration.TotalHours * chargingScheduleForThisHour.EstimatedSolarPower);
                                        chargingScheduleForThisHour.EstimatedSolarPower = 0;
                                    }
                                    else
                                    {
                                        var hoursToReduce = (double)tooMuchChargedEnergy / chargingScheduleForThisHour.EstimatedSolarPower;
                                        chargingScheduleForThisHour.ValidTo = chargingScheduleForThisHour.ValidTo.AddHours(-hoursToReduce);
                                        tooMuchChargedEnergy = 0;
                                    }
                                    i++;
                                }
                            }
                            _logger.LogDebug("Scheduled enough solar energy to reach target soc, so do not plan any further charging schedules");
                            break;
                        }
                    }
                }
            }
        }




        return schedules;
    }

    private int GetMinimumEnergyToCharge(DateTimeOffset currentDate, DtoTimeZonedChargingTarget nextTarget, DtoCar car,
        int? carUsableEnergy, List<DtoChargingSchedule> schedules)
    {
        int minimumEnergyToCharge = 0;
        if (nextTarget.TargetSoc != default)
        {
            var actualTargetSoc = GetActualTargetSoc(car.SocLimit.Value, nextTarget.TargetSoc, car.IsCharging.Value == true);
            if (actualTargetSoc != default && carUsableEnergy != default)
            {
                var energyToChargeWhileIgnoringExistingChargingSchedules = CalculateEnergyToCharge(
                    actualTargetSoc.Value,
                    car.SoC.Value ?? 0,
                    carUsableEnergy.Value);
                minimumEnergyToCharge = GetRemainingEnergyToCharge(currentDate, schedules, nextTarget.NextExecutionTime,
                    energyToChargeWhileIgnoringExistingChargingSchedules);
            }
        }

        return minimumEnergyToCharge;
    }


    /// <summary>
    /// Adds a new charging schedule to the existing schedules, adjusting power allocations to respect the specified
    /// maximum charging power.
    /// </summary>
    /// <remarks>Schedules are merged based on overlapping time intervals, and power allocations are capped at
    /// the specified maximum. Any energy that cannot be scheduled due to these constraints is reported in the return
    /// value.</remarks>
    /// <param name="existingSchedules">The list of existing charging schedules to which the new schedule will be added and potentially merged.</param>
    /// <param name="newChargingSchedule">The charging schedule to add. Its time boundaries and power requirements will be considered when merging with
    /// existing schedules.</param>
    /// <param name="maxChargingPower">The maximum allowed charging power, in watts, that must not be exceeded when combining schedules.</param>
    /// <returns>A tuple containing the updated list of charging schedules after adding the new schedule, and the remaining
    /// energy to charge that could not be allocated due to power constraints.</returns>
    private (List<DtoChargingSchedule> chargingSchedulesAfterPowerAdd, int additionalScheduledEnergy) AddChargingSchedule(List<DtoChargingSchedule> existingSchedules, DtoChargingSchedule newChargingSchedule,
        int maxChargingPower)
    {
        _logger.LogTrace("{method}({existingChargingSchedules}, {@newChargingSchedule}, {maxChargingPower})",
            nameof(AddChargingSchedule), existingSchedules, newChargingSchedule, maxChargingPower);
        var additionalScheduledEnergy = 0;
        var newScheduleDummyList = new List<DtoChargingSchedule>();
        newScheduleDummyList.Add(newChargingSchedule);
        var (splittedNewChedules, splittedExistingChargingSchedules) = _validFromToSplitter.SplitByBoundaries(newScheduleDummyList, existingSchedules, newChargingSchedule.ValidFrom, newChargingSchedule.ValidTo);
        foreach (var dtoChargingSchedule in splittedNewChedules)
        {
            var overlappingExistingChargingSchedule = splittedExistingChargingSchedules
                .FirstOrDefault(c => c.ValidFrom == dtoChargingSchedule.ValidFrom
                                     && c.ValidTo == dtoChargingSchedule.ValidTo);
            if (overlappingExistingChargingSchedule == default)
            {
                splittedExistingChargingSchedules.Add(dtoChargingSchedule);
                additionalScheduledEnergy += CalculateChargedEnergy(dtoChargingSchedule.ValidTo - dtoChargingSchedule.ValidFrom, dtoChargingSchedule.EstimatedChargingPower);
                continue;
            }

            var earlierEstimatedPower = overlappingExistingChargingSchedule.EstimatedChargingPower;
            overlappingExistingChargingSchedule.TargetMinPower = Math.Max(overlappingExistingChargingSchedule.TargetMinPower, dtoChargingSchedule.TargetMinPower);
            overlappingExistingChargingSchedule.EstimatedSolarPower = Math.Max(overlappingExistingChargingSchedule.EstimatedSolarPower, dtoChargingSchedule.EstimatedSolarPower);

            var addedPower = overlappingExistingChargingSchedule.EstimatedChargingPower - earlierEstimatedPower;
            additionalScheduledEnergy += CalculateChargedEnergy(dtoChargingSchedule.ValidTo - dtoChargingSchedule.ValidFrom, addedPower);
        }
        return (splittedExistingChargingSchedules, additionalScheduledEnergy);
    }

    private int GetRemainingEnergyToCharge(DateTimeOffset currentDate, List<DtoChargingSchedule> schedules,
        DateTimeOffset targetNextExecutionTime, int energyToChargeWhileIgnoringExistingChargingSchedules)
    {
        _logger.LogTrace("{method}({currentDate}, {@schedules}, {nextTargetNextExecution}, {energyToCharge})",
            nameof(GetRemainingEnergyToCharge), currentDate, schedules, targetNextExecutionTime, energyToChargeWhileIgnoringExistingChargingSchedules);
        var alreadyScheduledEnergy = 0;
        foreach (var schedule in schedules)
        {
            var actualStartDate = currentDate > schedule.ValidFrom
                ? currentDate
                : schedule.ValidFrom;
            var actualEndDate = targetNextExecutionTime < schedule.ValidTo
                ? targetNextExecutionTime
                : schedule.ValidTo;
            if (actualEndDate <= actualStartDate)
            {
                continue;
            }
            var durationHours = (actualEndDate - actualStartDate).TotalHours;
            var scheduledEnergy = (int)(durationHours * schedule.EstimatedChargingPower);
            alreadyScheduledEnergy += scheduledEnergy;
        }
        var restEnergyToCharge = energyToChargeWhileIgnoringExistingChargingSchedules - alreadyScheduledEnergy;
        return restEnergyToCharge;
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
                    c.MaximumPhases,
                    c.CarType,
                })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false)
            : null;

        var carSetting = _settings.Cars.FirstOrDefault(c => c.Id == carId);
        var carSoC = carSetting?.SoC.Value;
        var carPhases = carData?.CarType == CarType.Tesla ? carSetting?.ActualPhases : carData?.MaximumPhases;

        var maxPhases = CalculateMaxValue(connectorData?.ConnectedPhasesCount, carPhases);
        var maxCurrent = CalculateMaxValue(connectorData?.MaxCurrent, carData?.MaximumAmpere);
        var minPhases = CalculateMinValue(connectorData?.ConnectedPhasesCount, carPhases);
        var minCurrent = CalculateMinValue(connectorData?.MinCurrent, carData?.MinimumAmpere);


        return (carData?.UsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent);
    }

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

    /// <summary>
    /// Adds +1 to the target SOC if the car side SOC limit is equal to the charging target SOC to force the car to stop charging by itself.
    /// </summary>
    private int? GetActualTargetSoc(int? carSideSocLimit, int? chargingTargetTargetSoc, bool isCurrentlyCharging)
    {
        _logger.LogTrace("{method}({carSideSocLimit}, {chargingTargetTargetSoc}, {isCurrentlyCharging})", nameof(GetActualTargetSoc), carSideSocLimit, chargingTargetTargetSoc, isCurrentlyCharging);
        if (chargingTargetTargetSoc == default)
        {
            return null;
        }
        if ((carSideSocLimit == chargingTargetTargetSoc) && isCurrentlyCharging)
        {
            _logger.LogDebug("Car side SOC limit {carSideSocLimit} is equal to charging target SOC {chargingTargetTargetSoc} and car is currently charging. Incrementing target SOC by 1 to force car to stop charging by itself.", carSideSocLimit, chargingTargetTargetSoc);
            return chargingTargetTargetSoc + 1;
        }
        return chargingTargetTargetSoc;
    }

    private int CalculateEnergyToCharge(
        int chargingTargetSoc,
        int currentSoC,
        int usableEnergy)
    {
        _logger.LogTrace("{method}({chargingTargetSoc}, {currentSoC}, {usableEnergy})", nameof(CalculateEnergyToCharge), chargingTargetSoc, currentSoC, usableEnergy);
        var socDiff = chargingTargetSoc - currentSoC;
        var energyWh = socDiff * usableEnergy * 10; // soc*10 vs usableEnergy*1000 scale
        var energyLossFactor = 1 + (_configurationWrapper.CarChargeLoss() / (float)100);
        energyWh = (int)(energyWh * energyLossFactor);

        return energyWh > 0
            ? energyWh
            : default;
    }


    private int CalculateHomeBatteryEnergyToMinSoc()
    {
        _logger.LogTrace("{method}()", nameof(CalculateHomeBatteryEnergyToMinSoc));
        var homeBatteryEnergy = _configurationWrapper.HomeBatteryUsableEnergy();
        if (homeBatteryEnergy == default)
        {
            return 0;
        }
        var socDifference = _settings.HomeBatterySoc - _configurationWrapper.HomeBatteryMinSoc();
        if (socDifference > 0)
        {
            return socDifference.Value * homeBatteryEnergy.Value / 100;
        }
        return 0;
    }

    private int GetPowerAtPhasesAndCurrent(int phases, decimal current, int? voltage)
    {
        return (int)(phases * current * (voltage ?? 230));
    }

    private TimeSpan CalculateChargingDuration(
        int energyToChargeWh,
        double maxChargingPowerW)
    {
        // hours = Wh / W
        return TimeSpan.FromHours(energyToChargeWh / maxChargingPowerW);
    }

    private int CalculateChargedEnergy(
        TimeSpan chargingDuration,
        int chargingPower)
    {
        return (int)(chargingDuration.TotalHours * chargingPower);
    }
}
