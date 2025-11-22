using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
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
    private readonly IHomeBatteryEnergyCalculator _homeBatteryEnergyCalculator;
    private readonly ITscOnlyChargingCostService _tscOnlyChargingCostService;

    public ChargingScheduleService(ILogger<ChargingScheduleService> logger, ITeslaSolarChargerContext context,
        ISettings settings, IConfigurationWrapper configurationWrapper, IValidFromToSplitter validFromToSplitter,
        IConstants constants, IHomeBatteryEnergyCalculator homeBatteryEnergyCalculator,
        ITscOnlyChargingCostService tscOnlyChargingCostService)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _validFromToSplitter = validFromToSplitter;
        _constants = constants;
        _homeBatteryEnergyCalculator = homeBatteryEnergyCalculator;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
    }

    /// <summary>
    /// Adds +1 to the target SOC if the car side SOC limit is equal to the charging target SOC to force the car to stop charging by itself.
    /// </summary>
    public int? GetActualTargetSoc(int? carSideSocLimit, int? chargingTargetTargetSoc, bool isCurrentlyCharging)
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
            var homeBatteryEnergyToCharge = 0;
            if (minimumEnergyToCharge == 0)
            {
                if (_settings.HomeBatterySoc != default)
                {
                    var homeBatteryMinSocAtTime =
                        await _homeBatteryEnergyCalculator.GetHomeBatteryMinSocAtTime(nextTarget.NextExecutionTime, cancellationToken);
                    var estimatedHomeBatterySocAtTime =
                        await _homeBatteryEnergyCalculator.GetEstimatedHomeBatterySocAtTime(nextTarget.NextExecutionTime, _settings.HomeBatterySoc.Value, cancellationToken);
                    if (homeBatteryMinSocAtTime != default && estimatedHomeBatterySocAtTime != default)
                    {
                        homeBatteryEnergyToCharge = GetHomeBatteryEnergyFromSocDifference(estimatedHomeBatterySocAtTime.Value - homeBatteryMinSocAtTime.Value);
                    }
                }

                if (homeBatteryEnergyToCharge < 1)
                {
                    _logger.LogDebug("No energy to charge calculated for car {carId}. Do not plan charging schedule.", car.Id);
                    continue;
                }
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
                    (schedules, var additionalScheduledEnergy) = AddChargingSchedule(schedules, chargingScheduleToAdd, maxPower, minimumEnergyToCharge);
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
                            EstimatedSolarPower = maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Value,
                        };
                        (schedules, var additionalScheduledEnergy) = AddChargingSchedule(schedules, chargingScheduleToAdd, maxPower, minimumEnergyToCharge);
                        minimumEnergyToCharge -= additionalScheduledEnergy;
                    }
                }
            }

            if (nextTarget.DischargeHomeBatteryToMinSoc && homeBatteryEnergyToCharge > 0)
            {
                var homeBatteryDischargePower = _configurationWrapper.HomeBatteryDischargingPower();
                if (homeBatteryDischargePower > 0)
                {
                    var availableDischargePower = Math.Min(maxPower, homeBatteryDischargePower.Value);
                    _logger.LogTrace("Available discharge power: {availableDischargePower}W", availableDischargePower);
                    if (availableDischargePower > 0)
                    {
                        while (homeBatteryEnergyToCharge > 0)
                        {
                            var dischargeDuration = CalculateChargingDuration(homeBatteryEnergyToCharge, availableDischargePower);
                            var scheduleEnd = nextTarget.NextExecutionTime;
                            var scheduleStart = scheduleEnd - dischargeDuration;
                            if (scheduleStart < currentDate)
                            {
                                scheduleStart = currentDate;
                            }
                            _logger.LogTrace("Discharge duration: {dischargeDuration}; Scheduled end: {scheduledEnd}; scheduled start: {scheduledStart}",
                                dischargeDuration, scheduleEnd, scheduleStart);
                            if (scheduleStart < scheduleEnd)
                            {
                                var homeBatteryChargingSchedule = new DtoChargingSchedule(loadpoint.CarId.Value, loadpoint.ChargingConnectorId, maxPower)
                                {
                                    ValidFrom = scheduleStart,
                                    ValidTo = scheduleEnd,
                                    TargetHomeBatteryPower = availableDischargePower,
                                };
                                (schedules, var addedEnergy) = AddChargingSchedule(schedules, homeBatteryChargingSchedule, maxPower, homeBatteryEnergyToCharge);
                                homeBatteryEnergyToCharge -= addedEnergy;
                                minimumEnergyToCharge -= addedEnergy;
                                //As we want to discharge the complete home battery to min soc if DischargeHomeBatteryToMinSoc is set, we do not break here when minimumEnergyToCharge <= 0
                            }
                        }
                    }
                }
            }

            if (minimumEnergyToCharge <= 0)
            {
                return schedules;
            }

            schedules = await AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, minimumEnergyToCharge, maxPower);
        }
        return schedules;
    }

    private async Task<List<DtoChargingSchedule>> AppendOptimalGridSchedules(DateTimeOffset currentDate, DtoTimeZonedChargingTarget nextTarget,
        DtoLoadPointOverview loadpoint,
        List<DtoChargingSchedule> schedules, int minimumEnergyToCharge, int maxPower)
    {
        var electricityPrices = await _tscOnlyChargingCostService.GetPricesInTimeSpan(currentDate, nextTarget.NextExecutionTime);
        var endTimeOrderedElectricityPrices = electricityPrices.OrderBy(p => p.ValidTo).ToList();
        var lastGridPrice = endTimeOrderedElectricityPrices.LastOrDefault();
        if ((lastGridPrice == default) || (lastGridPrice.ValidTo < nextTarget.NextExecutionTime))
        {
            //Do not plan for target if last grid price is earlier than next execution time
            return schedules;
        }

        (var splittedGridPrices, schedules) =
            _validFromToSplitter.SplitByBoundaries(electricityPrices, schedules, currentDate, nextTarget.NextExecutionTime);
        var chargingSwitchCosts = _configurationWrapper.ChargingSwitchCosts();
        //Do not use car is charging here as also when it is preconditioning switching already happend
        var isCurrentlyCharging = loadpoint.ChargingPower > 0;
        var remainingEnergyToCoverFromGrid = minimumEnergyToCharge;
        var chargePricesIncludingSchedules = new Dictionary<int, (decimal chargeCost, List<DtoChargingSchedule> chargingSchedules)>();

        for (var startWithXCheapestPrice = 0; startWithXCheapestPrice < splittedGridPrices.Count; startWithXCheapestPrice++)
        {
            var serializedSchedules = JsonConvert.SerializeObject(schedules);
            var loopChargingSchedules = JsonConvert.DeserializeObject<List<DtoChargingSchedule>>(serializedSchedules);
            if (loopChargingSchedules == default)
            {
                throw new Exception("Could not deserialize charging schedules. This is an implentation error");
            }
            var i = 0;
            var chargingCosts = 0m;
            while (remainingEnergyToCoverFromGrid > 100)
            {
                var gridPriceOrderedElectricityPrices = GetOrderedElectricityPrices(currentDate, splittedGridPrices, isCurrentlyCharging, loopChargingSchedules, chargingSwitchCosts, maxPower);
                gridPriceOrderedElectricityPrices = gridPriceOrderedElectricityPrices.Where(p =>
                    !loopChargingSchedules.Any(c =>
                        c.ValidFrom == p.ValidFrom && c.ValidTo == p.ValidTo && c.TargetMinPower == maxPower)).ToList();
                var elementsToSkip = i++ == 0 ? startWithXCheapestPrice : 0;
                var cheapestPrice = gridPriceOrderedElectricityPrices.Skip(elementsToSkip).FirstOrDefault();
                if (cheapestPrice == default)
                {
                    break;
                }

                var chargingSchedule = new DtoChargingSchedule(loadpoint.CarId, loadpoint.ChargingConnectorId, maxPower)
                {
                    ValidFrom = cheapestPrice.ValidFrom,
                    ValidTo = cheapestPrice.ValidTo,
                    TargetMinPower = maxPower,
                };
                (loopChargingSchedules, var addedEnergy) = AddChargingSchedule(loopChargingSchedules, chargingSchedule, maxPower, remainingEnergyToCoverFromGrid);
                remainingEnergyToCoverFromGrid -= addedEnergy;
                chargingCosts += (addedEnergy / 1000m) * cheapestPrice.GridPrice;
            }
            chargePricesIncludingSchedules[startWithXCheapestPrice] = (chargingCosts, loopChargingSchedules);
        }
        schedules = chargePricesIncludingSchedules.OrderBy(c => c.Value.chargeCost).FirstOrDefault().Value.chargingSchedules;
        return schedules;
    }

    private List<Price> GetOrderedElectricityPrices(DateTimeOffset currentDate, List<Price> splittedGridPrices, bool isCurrentlyCharging,
        List<DtoChargingSchedule> splittedChargingSchedules, decimal chargingSwitchCosts, int maxPower)
    {
        var gridPricesIncludingCorrections = new List<Price>();
        foreach (var gridPrice in splittedGridPrices)
        {
            var gridPriceCopy = GetCopy(gridPrice);
            if (gridPrice.ValidFrom <= currentDate && gridPrice.ValidTo > currentDate)
            {
                if (isCurrentlyCharging)
                {
                    gridPricesIncludingCorrections.Add(gridPriceCopy);
                    continue;
                }
            }
            var forSureChargingChargingSchedules = splittedChargingSchedules
                .Where(c => c.TargetMinPower > 0).ToList();
            if (forSureChargingChargingSchedules.Any(c =>
                    c.ValidFrom == gridPrice.ValidTo
                    || c.ValidTo == gridPrice.ValidFrom
                    || (c.ValidFrom == gridPrice.ValidFrom && c.ValidTo == gridPrice.ValidTo)))
            {
                gridPricesIncludingCorrections.Add(gridPriceCopy);
                continue;
            }
            var switchCostsPerKwh = (chargingSwitchCosts / (decimal)(maxPower * (gridPriceCopy.ValidTo - gridPrice.ValidFrom).TotalHours)) * 1000m;
            gridPriceCopy.GridPrice += switchCostsPerKwh;
            gridPricesIncludingCorrections.Add(gridPriceCopy);
        }

        var gridPriceOrderedElectricityPrices = gridPricesIncludingCorrections
            .OrderBy(p => p.GridPrice)
            .ThenByDescending(p => p.ValidFrom)
            .ToList();
        return gridPriceOrderedElectricityPrices;
    }

    private Price GetCopy(Price oldPrice)
    {
        return new Price()
        {
            GridPrice = oldPrice.GridPrice,
            SolarPrice = oldPrice.SolarPrice,
            ValidFrom = new DateTimeOffset(oldPrice.ValidFrom.UtcDateTime, TimeSpan.Zero),
            ValidTo = new DateTimeOffset(oldPrice.ValidTo.UtcDateTime, TimeSpan.Zero),
        };
    }

    private int GetMinimumEnergyToCharge(DateTimeOffset currentDate, DtoTimeZonedChargingTarget nextTarget, DtoCar car,
        int? carUsableEnergy, List<DtoChargingSchedule> schedules)
    {
        var minimumEnergyToCharge = 0;
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
    /// Adds a new charging schedule to the existing list, ensuring that the total scheduled energy does not exceed the
    /// specified maximum and that charging power constraints are respected.
    /// </summary>
    /// <remarks>If the new charging schedule overlaps with existing schedules, the method merges or adjusts
    /// the schedules to respect the specified power and energy limits. The returned list may contain modified schedules
    /// to ensure that constraints are not violated.</remarks>
    /// <param name="existingSchedules">The list of existing charging schedules to which the new schedule will be added. This list may be modified to
    /// accommodate the new schedule and resolve any overlaps.</param>
    /// <param name="newChargingSchedule">The charging schedule to add. Defines the time window and power requirements for the new charging session.</param>
    /// <param name="maxChargingPower">The maximum allowed charging power, in watts, for any schedule slot. Used to limit the charging power assigned
    /// to the new or updated schedules.</param>
    /// <param name="maxEnergyToAdd">The maximum additional energy, in watt-hours, that can be scheduled by adding the new charging schedule. The
    /// method will not exceed this limit when updating the schedules.</param>
    /// <returns>A tuple containing the updated list of charging schedules after adding the new schedule, and the total
    /// additional scheduled energy in watt-hours. The list reflects any adjustments made to comply with power and
    /// energy constraints.</returns>
    private (List<DtoChargingSchedule> chargingSchedulesAfterPowerAdd, int additionalScheduledEnergy) AddChargingSchedule(
    List<DtoChargingSchedule> existingSchedules,
    DtoChargingSchedule newChargingSchedule,
    int maxChargingPower,
    int maxEnergyToAdd)
    {
        _logger.LogTrace("{method}({existingChargingSchedules}, {@newChargingSchedule}, {maxChargingPower}, {maxEnergyToAdd})",
            nameof(AddChargingSchedule), existingSchedules, newChargingSchedule, maxChargingPower, maxEnergyToAdd);
        var additionalScheduledEnergy = 0;
        var newScheduleDummyList = new List<DtoChargingSchedule>();
        newScheduleDummyList.Add(newChargingSchedule);
        var (splittedNewChedules, splittedExistingChargingSchedules)
            = _validFromToSplitter.SplitByBoundaries(newScheduleDummyList, existingSchedules, newChargingSchedule.ValidFrom, newChargingSchedule.ValidTo);

        foreach (var dtoChargingSchedule in splittedNewChedules)
        {
            var overlappingExistingChargingSchedule = splittedExistingChargingSchedules
                .FirstOrDefault(c => c.ValidFrom == dtoChargingSchedule.ValidFrom
                                     && c.ValidTo == dtoChargingSchedule.ValidTo);
            if (overlappingExistingChargingSchedule == default)
            {
                var slotEnergy = CalculateChargedEnergy(dtoChargingSchedule.ValidTo - dtoChargingSchedule.ValidFrom, dtoChargingSchedule.EstimatedChargingPower);
                var energyToAdd = Math.Min(slotEnergy, maxEnergyToAdd - additionalScheduledEnergy);

                if (energyToAdd <= 0)
                {
                    break;
                }

                // If we need to add partial energy, adjust the power proportionally
                if (energyToAdd < slotEnergy)
                {
                    var hoursToReduce = (slotEnergy - energyToAdd) / (double)dtoChargingSchedule.EstimatedChargingPower;
                    if (splittedExistingChargingSchedules.Any(s => s.ValidTo == dtoChargingSchedule.ValidFrom))
                    {
                        dtoChargingSchedule.ValidTo = dtoChargingSchedule.ValidTo.AddHours(-hoursToReduce);
                    }
                    else if (splittedExistingChargingSchedules.Any(s => s.ValidFrom == dtoChargingSchedule.ValidTo))
                    {
                        dtoChargingSchedule.ValidFrom = dtoChargingSchedule.ValidFrom.AddHours(hoursToReduce);
                    }
                }

                splittedExistingChargingSchedules.Add(dtoChargingSchedule);
                additionalScheduledEnergy += energyToAdd;
                continue;
            }

            var earlierEstimatedPower = overlappingExistingChargingSchedule.EstimatedChargingPower;

            var newMinTargetPower = Math.Max(overlappingExistingChargingSchedule.TargetMinPower, dtoChargingSchedule.TargetMinPower);
            var newEstimatedSolarPower = Math.Max(overlappingExistingChargingSchedule.EstimatedSolarPower, dtoChargingSchedule.EstimatedSolarPower);

            overlappingExistingChargingSchedule.TargetMinPower = newMinTargetPower;
            overlappingExistingChargingSchedule.EstimatedSolarPower = newEstimatedSolarPower;

            var addedPower = overlappingExistingChargingSchedule.EstimatedChargingPower - earlierEstimatedPower;
            var slotAddedEnergy = CalculateChargedEnergy(dtoChargingSchedule.ValidTo - dtoChargingSchedule.ValidFrom, addedPower);
            var energyToAdd1 = Math.Min(slotAddedEnergy, maxEnergyToAdd - additionalScheduledEnergy);

            if (energyToAdd1 < slotAddedEnergy && energyToAdd1 > 0)
            {
                var hoursToReduce = (slotAddedEnergy - energyToAdd1) / (double)dtoChargingSchedule.EstimatedChargingPower;
                if (splittedExistingChargingSchedules.Any(s => s.ValidTo == dtoChargingSchedule.ValidFrom))
                {
                    dtoChargingSchedule.ValidTo = dtoChargingSchedule.ValidTo.AddHours(-hoursToReduce);
                }
                else if (splittedExistingChargingSchedules.Any(s => s.ValidFrom == dtoChargingSchedule.ValidTo))
                {
                    dtoChargingSchedule.ValidFrom = dtoChargingSchedule.ValidFrom.AddHours(hoursToReduce);
                }
                // Need to reduce the power increase to stay within limit
                var timeSpan = dtoChargingSchedule.ValidTo - dtoChargingSchedule.ValidFrom;
                var limitedAddedPower = (int)(energyToAdd1 / (timeSpan.TotalHours));
                overlappingExistingChargingSchedule.TargetMinPower = earlierEstimatedPower + limitedAddedPower;
            }

            additionalScheduledEnergy += energyToAdd1;

            if (additionalScheduledEnergy >= maxEnergyToAdd)
            {
                break; // Reached the limit
            }
        }
        return (splittedExistingChargingSchedules, additionalScheduledEnergy);
    }

    private enum EnergyCutOffType
    {
        Start,
        End,
        ReducePower,
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


    private int GetHomeBatteryEnergyFromSocDifference(int socDifference)
    {
        _logger.LogTrace("{method}()", nameof(GetHomeBatteryEnergyFromSocDifference));
        var homeBatteryEnergy = _configurationWrapper.HomeBatteryUsableEnergy();
        if (homeBatteryEnergy == default)
        {
            return 0;
        }
        if (socDifference > 0)
        {
            return socDifference * homeBatteryEnergy.Value / 100;
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
