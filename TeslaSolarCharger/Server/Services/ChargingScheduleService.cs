using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
            _logger.LogDebug("No schedules generated because loadpoint {carId}_{connectorId} has no car assigned.", loadpoint.CarId, loadpoint.ChargingConnectorId);
            return schedules;
        }
        var car = _settings.Cars.First(c => c.Id == loadpoint.CarId.Value);
        _logger.LogTrace("Using car {@car} for loadpoint {carId}_{connectorId}.", car, loadpoint.CarId, loadpoint.ChargingConnectorId);
        if (car.ChargeModeV2 != ChargeModeV2.Auto)
        {
            _logger.LogDebug("No schedules generated because car {carId} is not in Auto mode. Current mode: {mode}", car.Id, car.ChargeModeV2);
            return schedules;
        }
        var (carUsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent) = await GetChargingScheduleRelevantData(loadpoint.CarId, loadpoint.ChargingConnectorId).ConfigureAwait(false);
        _logger.LogTrace("Charging schedule relevant data for car {carId}: carUsableEnergy={carUsableEnergy}, carSoC={carSoC}, maxPhases={maxPhases}, maxCurrent={maxCurrent}, minPhases={minPhases}, minCurrent={minCurrent}", car.Id, carUsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent);
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
            _logger.LogTrace("Minimum energy to charge for target {@target}: {minimumEnergyToCharge}Wh", nextTarget, minimumEnergyToCharge);
            var homeBatteryEnergyToCharge = 0;
            if (nextTarget.DischargeHomeBatteryToMinSoc)
            {
                _logger.LogTrace("Target {@target} requests discharge of home battery to min SoC.", nextTarget);
                if (_settings.HomeBatterySoc != default)
                {
                    var homeBatteryMinSocAtTime =
                        await _homeBatteryEnergyCalculator.GetHomeBatteryMinSocAtTime(nextTarget.NextExecutionTime, cancellationToken);
                    var estimatedHomeBatterySocAtTime =
                        await _homeBatteryEnergyCalculator.GetEstimatedHomeBatterySocAtTime(nextTarget.NextExecutionTime, _settings.HomeBatterySoc.Value, cancellationToken);
                    _logger.LogTrace("Home battery SoC data for target {@target}: minSoCAtTime={minSoC}, estimatedSoCAtTime={estimatedSoC}", nextTarget, homeBatteryMinSocAtTime, estimatedHomeBatterySocAtTime);
                    if (homeBatteryMinSocAtTime != default && estimatedHomeBatterySocAtTime != default)
                    {
                        homeBatteryEnergyToCharge = GetHomeBatteryEnergyFromSocDifference(estimatedHomeBatterySocAtTime.Value - homeBatteryMinSocAtTime.Value);
                        _logger.LogTrace("Calculated home battery energy to charge: {homeBatteryEnergyToCharge}Wh", homeBatteryEnergyToCharge);
                    }
                }

                if (homeBatteryEnergyToCharge < 1 && minimumEnergyToCharge == 0)
                {
                    _logger.LogDebug("No energy to charge calculated for car {carId}. Do not plan charging schedule.", car.Id);
                    continue;
                }
            }

            var maxPower = GetPowerAtPhasesAndCurrent(maxPhases.Value, maxCurrent.Value, loadpoint.EstimatedVoltageWhileCharging);
            _logger.LogTrace("Max charging power at loadpoint {carId}_{connectorId}: {maxPower}W", loadpoint.CarId, loadpoint.ChargingConnectorId, maxPower);
            //Target was not reached in time so schedule full power until target is reached
            if (nextTarget.NextExecutionTime < currentDate)
            {
                _logger.LogDebug("Next target {@nextTarget} is in the past. Plan charging immediatly.", nextTarget);
                var startDate = currentDate;
                while (minimumEnergyToCharge > 100)
                {
                    var chargingDuration = CalculateChargingDuration(minimumEnergyToCharge, maxPower);
                    var validToDate = startDate + chargingDuration;
                    _logger.LogTrace("Immediate charging iteration for car {carId}: start={startDate}, end={validToDate}, remainingEnergy={remainingEnergy}", car.Id, startDate, validToDate, minimumEnergyToCharge);
                    var chargingScheduleToAdd = new DtoChargingSchedule(loadpoint.CarId, loadpoint.ChargingConnectorId, maxPower, [ScheduleReason.LatestPossibleTime,])
                    {
                        ValidFrom = startDate,
                        ValidTo = validToDate,
                        TargetMinPower = maxPower,
                    };
                    (schedules, var additionalScheduledEnergy) = AddChargingSchedule(schedules, chargingScheduleToAdd, maxPower, minimumEnergyToCharge);
                    _logger.LogTrace("Added immediate charging schedule for car {carId}. Additional scheduled energy: {additionalScheduledEnergy}Wh", car.Id, additionalScheduledEnergy);
                    minimumEnergyToCharge -= additionalScheduledEnergy;
                    startDate = new(validToDate.Year, validToDate.Month, validToDate.Day, validToDate.Hour,
                        validToDate.Minute, validToDate.Second, validToDate.Offset);
                }
                _logger.LogTrace("Finished immediate planning for past target {@nextTarget}. Remaining minimumEnergyToCharge={minimumEnergyToCharge}", nextTarget, minimumEnergyToCharge);
                continue;
            }

            if (_configurationWrapper.UsePredictedSolarPowerGenerationForChargingSchedules())
            {
                _logger.LogTrace("Using predicted solar power generation for charging schedules for target {@target}.", nextTarget);
                if (minPhases == default || minCurrent == default)
                {
                    _logger.LogWarning("Can not schedule based on solar predictions as minPhases or minCurrent is not set");
                }
                else
                {
                    var minPower = GetPowerAtPhasesAndCurrent(minPhases.Value, minCurrent.Value, loadpoint.EstimatedVoltageWhileCharging);
                    _logger.LogTrace("Min charging power for predicted solar scheduling: {minPower}W", minPower);
                    var maxPowerCappedPredictedHoursWithAtLeastMinPowerSurpluses = predictedSurplusSlices
                        .Where(s => s.Value > minPower && s.Key < nextTarget.NextExecutionTime)
                        .OrderBy(s => s.Key)
                        .ToDictionary(s => s.Key, s => s.Value > maxPower ? maxPower : s.Value);
                    _logger.LogTrace("Found {count} predicted surplus slices with at least min power for target {@target}.", maxPowerCappedPredictedHoursWithAtLeastMinPowerSurpluses.Count, nextTarget);
                    foreach (var maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus in maxPowerCappedPredictedHoursWithAtLeastMinPowerSurpluses)
                    {
                        var startDate = maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key < currentDate
                            ? currentDate
                            : maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key;
                        var endDate =
                            maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key.AddHours(_constants.SolarPowerSurplusPredictionIntervalHours) > nextTarget.NextExecutionTime
                                ? nextTarget.NextExecutionTime
                                : maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Key.AddHours(_constants.SolarPowerSurplusPredictionIntervalHours);
                        _logger.LogTrace("Solar-based schedule window for car {carId}: start={startDate}, end={endDate}, estimatedSolarPower={estimatedSolarPower}", car.Id, startDate, endDate, maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Value);
                        var chargingScheduleToAdd = new DtoChargingSchedule(loadpoint.CarId.Value, loadpoint.ChargingConnectorId, maxPower, [ScheduleReason.ExpectedSolarProduction])
                        {
                            ValidFrom = startDate,
                            ValidTo = endDate,
                            EstimatedSolarPower = maxPowerCappedPredictedHoursWithAtLeastMinPowerSurplus.Value,
                        };
                        (schedules, var additionalScheduledEnergy) = AddChargingSchedule(schedules, chargingScheduleToAdd, maxPower, minimumEnergyToCharge);
                        _logger.LogTrace("Added solar-based charging schedule for car {carId}. Additional scheduled energy: {additionalScheduledEnergy}Wh", car.Id, additionalScheduledEnergy);
                        minimumEnergyToCharge -= additionalScheduledEnergy;
                    }
                }
            }
            else
            {
                _logger.LogTrace("Predicted solar power generation is not used for charging schedules for target {@target}.", nextTarget);
            }

            if (nextTarget.DischargeHomeBatteryToMinSoc && homeBatteryEnergyToCharge > 0)
            {
                var homeBatteryDischargePower = _configurationWrapper.HomeBatteryDischargingPower();
                _logger.LogTrace("Home battery discharge requested. Configured discharge power: {homeBatteryDischargePower}", homeBatteryDischargePower);
                if (homeBatteryDischargePower > 0)
                {
                    var availableDischargePower = Math.Min(maxPower, homeBatteryDischargePower.Value);
                    _logger.LogTrace("Available discharge power: {availableDischargePower}W", availableDischargePower);
                    if (availableDischargePower > 0)
                    {
                        while (homeBatteryEnergyToCharge > 100)
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
                                var homeBatteryChargingSchedule = new DtoChargingSchedule(loadpoint.CarId.Value, loadpoint.ChargingConnectorId, maxPower, [ScheduleReason.HomeBatteryDischarging])
                                {
                                    ValidFrom = scheduleStart,
                                    ValidTo = scheduleEnd,
                                    TargetHomeBatteryPower = availableDischargePower,
                                };
                                (schedules, var addedEnergy) = AddChargingSchedule(schedules, homeBatteryChargingSchedule, maxPower, homeBatteryEnergyToCharge);
                                _logger.LogTrace("Added home battery discharge schedule. AddedEnergy={addedEnergy}Wh; Remaining homeBatteryEnergyToCharge before subtract={remainingEnergy}", addedEnergy, homeBatteryEnergyToCharge);
                                homeBatteryEnergyToCharge -= addedEnergy;
                                minimumEnergyToCharge -= addedEnergy;
                                //As we want to discharge the complete home battery to min soc if DischargeHomeBatteryToMinSoc is set, we do not break here when minimumEnergyToCharge <= 0
                            }
                            else
                            {
                                _logger.LogTrace("Skipping home battery discharge schedule because scheduleStart >= scheduleEnd (start={scheduleStart}, end={scheduleEnd})", scheduleStart, scheduleEnd);
                            }
                        }
                        _logger.LogTrace("Finished home battery discharge planning for target {@target}. Remaining homeBatteryEnergyToCharge={homeBatteryEnergyToCharge}, minimumEnergyToCharge={minimumEnergyToCharge}", nextTarget, homeBatteryEnergyToCharge, minimumEnergyToCharge);
                    }
                    else
                    {
                        _logger.LogTrace("No available discharge power for home battery discharge scheduling.");
                    }
                }
                else
                {
                    _logger.LogTrace("Configured home battery discharge power is not greater than zero. No discharge scheduling performed.");
                }
            }
            else
            {
                _logger.LogTrace("No home battery discharge requested or no energy to charge for target {@target}. homeBatteryEnergyToCharge={homeBatteryEnergyToCharge}", nextTarget, homeBatteryEnergyToCharge);
            }

            schedules = await AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, minimumEnergyToCharge, maxPower);
            if (minPhases != default && minCurrent != default)
            {
                var minChargingPower = GetPowerAtPhasesAndCurrent(minPhases.Value, minCurrent.Value, loadpoint.EstimatedVoltageWhileCharging);
                var isCurrentlyCharging = loadpoint.ChargingPower > 0;

                schedules = OptimizeChargingSchedules(schedules, currentDate, isCurrentlyCharging, minChargingPower);
            }
            _logger.LogTrace("Schedules after AppendOptimalGridSchedules for target {@target}: {scheduleCount} schedules.", nextTarget, schedules.Count);
        }
        _logger.LogTrace("Finished GenerateChargingSchedulesForLoadPoint for loadpoint {carId}_{connectorId}. Total schedules: {scheduleCount}", loadpoint.CarId, loadpoint.ChargingConnectorId, schedules.Count);
        return schedules;
    }

    private async Task<List<DtoChargingSchedule>> AppendOptimalGridSchedules(DateTimeOffset currentDate, DtoTimeZonedChargingTarget nextTarget,
        DtoLoadPointOverview loadpoint,
        List<DtoChargingSchedule> schedules, int energyToCharge, int maxPower)
    {
        _logger.LogTrace("AppendOptimalGridSchedules called for loadpoint {carId}_{connectorId}, energyToCharge={energyToCharge}, maxPower={maxPower}, currentDate={currentDate}, nextExecutionTime={nextExecutionTime}", loadpoint.CarId, loadpoint.ChargingConnectorId, energyToCharge, maxPower, currentDate, nextTarget.NextExecutionTime);
        var electricityPrices = await _tscOnlyChargingCostService.GetPricesInTimeSpan(currentDate, nextTarget.NextExecutionTime);
        _logger.LogTrace("Retrieved {priceCount} electricity price slices for grid scheduling between {currentDate} and {nextExecutionTime}", electricityPrices.Count, currentDate, nextTarget.NextExecutionTime);
        var endTimeOrderedElectricityPrices = electricityPrices.OrderBy(p => p.ValidTo).ToList();
        var lastGridPrice = endTimeOrderedElectricityPrices.LastOrDefault();
        if ((lastGridPrice == default) || (lastGridPrice.ValidTo < nextTarget.NextExecutionTime))
        {
            _logger.LogWarning("Can not plan for target {@nextTarget} because last grid price is missing or earlier than next execution time. lastGridPrice={@lastGridPrice}", nextTarget, lastGridPrice);
            //Do not plan for target if last grid price is earlier than next execution time
            return schedules;
        }

        (var splittedGridPrices, schedules) =
            _validFromToSplitter.SplitByBoundaries(electricityPrices, schedules, currentDate, nextTarget.NextExecutionTime, false);
        _logger.LogTrace("Split grid prices into {splittedCount} segments for optimal scheduling. Existing schedules count after split: {scheduleCount}", splittedGridPrices.Count, schedules.Count);
        var chargingSwitchCosts = _configurationWrapper.ChargingSwitchCosts();
        _logger.LogTrace("Charging switch costs: {chargingSwitchCosts}", chargingSwitchCosts);
        //Do not use car is charging here as also when it is preconditioning switching already happend
        var isCurrentlyCharging = loadpoint.ChargingPower > 0;
        _logger.LogTrace("Is currently charging at loadpoint {carId}_{connectorId}: {isCurrentlyCharging}, currentChargingPower={chargingPower}", loadpoint.CarId, loadpoint.ChargingConnectorId, isCurrentlyCharging, loadpoint.ChargingPower);
        // CHANGED: Value tuple now includes 'int remainingEnergy' to track if the schedule was fulfilled
        var chargePricesIncludingSchedules = new Dictionary<int, (decimal chargeCost, List<DtoChargingSchedule> chargingSchedules, int remainingEnergy)>();

        for (var startWithXCheapestPrice = 0; startWithXCheapestPrice < splittedGridPrices.Count; startWithXCheapestPrice++)
        {
            _logger.LogTrace("Start evaluating grid schedule option with startWithXCheapestPrice={startWithXCheapestPrice}", startWithXCheapestPrice);
            var remainingEnergyToCoverFromGrid = energyToCharge;
            var serializedSchedules = JsonConvert.SerializeObject(schedules);
            _logger.LogTrace("Serialized base schedules for cloning. Length={length}", serializedSchedules.Length);
            var loopChargingSchedules = JsonConvert.DeserializeObject<List<DtoChargingSchedule>>(serializedSchedules);
            if (loopChargingSchedules == default)
            {
                _logger.LogError("Could not deserialize charging schedules. This is an implentation error");
                throw new Exception("Could not deserialize charging schedules. This is an implentation error");
            }
            _logger.LogTrace("Deserialized loopChargingSchedules. Count={loopScheduleCount}", loopChargingSchedules.Count);
            var i = 0;
            var chargingCosts = 0m;
            while (remainingEnergyToCoverFromGrid > 100)
            {
                var gridPriceOrderedElectricityPrices = GetOrderedElectricityPrices(currentDate, splittedGridPrices, isCurrentlyCharging, loopChargingSchedules, chargingSwitchCosts, maxPower);
                _logger.LogTrace("Ordered {count} grid price slices by effective cost for current iteration. remainingEnergyToCoverFromGrid={remainingEnergyToCoverFromGrid}", gridPriceOrderedElectricityPrices.Count, remainingEnergyToCoverFromGrid);
                gridPriceOrderedElectricityPrices = gridPriceOrderedElectricityPrices.Where(p =>
                    !loopChargingSchedules.Any(c =>
                        c.ValidFrom == p.ValidFrom && c.ValidTo == p.ValidTo && c.TargetMinPower == maxPower)).ToList();
                _logger.LogTrace("Filtered grid price slices to {count} options that are not already fully scheduled.", gridPriceOrderedElectricityPrices.Count);
                var elementsToSkip = i++ == 0 ? startWithXCheapestPrice : 0;
                var cheapestPrice = gridPriceOrderedElectricityPrices.Skip(elementsToSkip).FirstOrDefault();
                if (cheapestPrice == default)
                {
                    _logger.LogDebug("No more grid price slots available for scheduling with startWithXCheapestPrice={startWithXCheapestPrice}. RemainingEnergyToCoverFromGrid={remainingEnergyToCoverFromGrid}", startWithXCheapestPrice, remainingEnergyToCoverFromGrid);
                    // Break the loop; we will handle the deficit (overflow) after selecting the best option
                    break;
                }

                _logger.LogTrace("Selected cheapest grid price slot: ValidFrom={validFrom}, ValidTo={validTo}, GridPrice={gridPrice}, elementsToSkip={elementsToSkip}", cheapestPrice.ValidFrom, cheapestPrice.ValidTo, cheapestPrice.GridPrice, elementsToSkip);
                var chargingSchedule = new DtoChargingSchedule(loadpoint.CarId, loadpoint.ChargingConnectorId, maxPower, [ScheduleReason.CheapGridPrice])
                {
                    ValidFrom = cheapestPrice.ValidFrom,
                    ValidTo = cheapestPrice.ValidTo,
                    TargetMinPower = maxPower,
                };
                (loopChargingSchedules, var addedEnergy) = AddChargingSchedule(loopChargingSchedules, chargingSchedule, maxPower, remainingEnergyToCoverFromGrid);
                _logger.LogTrace("Added grid-based charging schedule: AddedEnergy={addedEnergy}Wh, ValidFrom={validFrom}, ValidTo={validTo}", addedEnergy, chargingSchedule.ValidFrom, chargingSchedule.ValidTo);
                remainingEnergyToCoverFromGrid -= addedEnergy;
                var additionalCosts = (addedEnergy / 1000m) * cheapestPrice.GridPrice;
                chargingCosts += additionalCosts;
                _logger.LogTrace("Updated grid charging costs: added={additionalCosts}, total={chargingCosts}, remainingEnergyToCoverFromGrid={remainingEnergyToCoverFromGrid}", additionalCosts, chargingCosts, remainingEnergyToCoverFromGrid);
            }
            _logger.LogTrace("Finished evaluation for startWithXCheapestPrice={startWithXCheapestPrice}. TotalChargingCosts={chargingCosts}, resultingSchedulesCount={loopScheduleCount}", startWithXCheapestPrice, chargingCosts, loopChargingSchedules.Count);
            // CHANGED: Store remainingEnergyToCoverFromGrid in the dictionary
            chargePricesIncludingSchedules[startWithXCheapestPrice] = (chargingCosts, loopChargingSchedules, remainingEnergyToCoverFromGrid);
        }

        _logger.LogTrace("Completed evaluation of all grid schedule options. Option count={optionCount}", chargePricesIncludingSchedules.Count);

        // CHANGED: Select the best option and check for remaining energy
        var bestOption = chargePricesIncludingSchedules.OrderBy(c => c.Value.chargeCost).FirstOrDefault().Value;
        schedules = bestOption.chargingSchedules;
        var finalRemainingEnergy = bestOption.remainingEnergy;

        // FIX: If there is still energy to charge after optimizing available grid slots, schedule it immediately after NextExecutionTime
        if (finalRemainingEnergy > 100)
        {
            _logger.LogWarning("Time window until {nextExecutionTime} was insufficient to charge required energy. Scheduling remaining {remaining}Wh after target time.", nextTarget.NextExecutionTime, finalRemainingEnergy);

            var overflowStartDate = nextTarget.NextExecutionTime;

            // Ensure we don't start in the past if NextExecutionTime is weirdly configured, though main logic prevents this usually
            if (overflowStartDate < currentDate)
                overflowStartDate = currentDate;

            while (finalRemainingEnergy > 100)
            {
                var chargingDuration = CalculateChargingDuration(finalRemainingEnergy, maxPower);
                var validToDate = overflowStartDate + chargingDuration;

                var overflowSchedule = new DtoChargingSchedule(loadpoint.CarId, loadpoint.ChargingConnectorId, maxPower, [ScheduleReason.LatestPossibleTime])
                {
                    ValidFrom = overflowStartDate,
                    ValidTo = validToDate,
                    TargetMinPower = maxPower,
                };

                (schedules, var addedEnergy) = AddChargingSchedule(schedules, overflowSchedule, maxPower, finalRemainingEnergy);

                _logger.LogTrace("Added overflow schedule: ValidFrom={validFrom}, ValidTo={validTo}, AddedEnergy={addedEnergy}", overflowStartDate, validToDate, addedEnergy);

                finalRemainingEnergy -= addedEnergy;

                // Advance start date in case AddChargingSchedule split the schedule or we need multiple iterations (unlikely with simple duration calc, but safe)
                overflowStartDate = new DateTimeOffset(validToDate.Year, validToDate.Month, validToDate.Day, validToDate.Hour, validToDate.Minute, validToDate.Second, validToDate.Offset);
            }
        }

        _logger.LogTrace("Selected optimal grid schedules option. Final schedule count={scheduleCount}", schedules.Count);
        return schedules;
    }

    private List<DtoChargingSchedule> OptimizeChargingSchedules(List<DtoChargingSchedule> schedules,
    DateTimeOffset currentDate, bool isCurrentlyCharging, int minChargingPower)
    {
        _logger.LogTrace("Starting schedule optimization. Input count: {count}", schedules.Count);

        if (!schedules.Any())
        {
            return schedules;
        }

        // 1. Sort schedules by start time to ensure correct processing
        var sortedSchedules = schedules.OrderBy(s => s.ValidFrom).ToList();
        var filledSchedules = new List<DtoChargingSchedule>();

        // 2. Handle Leading Edge (Current Charging)
        // If the loadpoint is currently charging, we want to bridge the gap from 'now' to the first schedule
        var maxGapToFill = TimeSpan.FromMinutes(20);
        if (isCurrentlyCharging)
        {
            var firstSchedule = sortedSchedules.First();
            if (firstSchedule.ValidFrom > currentDate)
            {
                var gap = firstSchedule.ValidFrom - currentDate;
                if (gap <= maxGapToFill && gap > TimeSpan.Zero)
                {
                    _logger.LogTrace("Filling leading gap for currently charging loadpoint. Gap: {gap}", gap);
                    var leadingSchedule = new DtoChargingSchedule(firstSchedule.CarId, firstSchedule.OcppChargingConnectorId, firstSchedule.MaxPossiblePower, [ScheduleReason.BridgeSchedules])
                    {
                        ValidFrom = currentDate,
                        ValidTo = firstSchedule.ValidFrom,
                        TargetMinPower = minChargingPower,
                    };
                    filledSchedules.Add(leadingSchedule);
                }
            }
        }

        // 3. Fill internal gaps <= 20 minutes
        for (var i = 0; i < sortedSchedules.Count; i++)
        {
            filledSchedules.Add(sortedSchedules[i]);

            if (i < sortedSchedules.Count - 1)
            {
                var current = sortedSchedules[i];
                var next = sortedSchedules[i + 1];

                // Ensure we only fill if there is an actual gap (next starts after current ends)
                if (next.ValidFrom > current.ValidTo)
                {
                    var gap = next.ValidFrom - current.ValidTo;
                    if (gap <= maxGapToFill)
                    {
                        _logger.LogTrace("Filling gap between schedules. Gap: {gap}, From: {from}, To: {to}", gap, current.ValidTo, next.ValidFrom);
                        var fillerSchedule = new DtoChargingSchedule(current.CarId, current.OcppChargingConnectorId, current.MaxPossiblePower, [ScheduleReason.BridgeSchedules])
                        {
                            ValidFrom = current.ValidTo,
                            ValidTo = next.ValidFrom,
                            TargetMinPower = minChargingPower,
                        };
                        filledSchedules.Add(fillerSchedule);
                    }
                }
            }
        }

        // 4. Merge contiguous schedules with identical power values
        var mergedSchedules = new List<DtoChargingSchedule>();
        if (filledSchedules.Any())
        {
            var current = filledSchedules[0];
            for (var i = 1; i < filledSchedules.Count; i++)
            {
                var next = filledSchedules[i];

                // Schedules are candidates for merge if they are contiguous (allow for minimal tolerance or exact match)
                // and possess identical power parameters
                var isContiguous = current.ValidTo == next.ValidFrom;

                if (isContiguous && AreSchedulesMergeable(current, next))
                {
                    // Merge: Extend the current schedule to the end of the next one
                    _logger.LogTrace("Merging contiguous schedules. Start: {start}, NewEnd: {end}, Power: {power}", current.ValidFrom, next.ValidTo, current.TargetMinPower);
                    current.ValidTo = next.ValidTo;
                }
                else
                {
                    mergedSchedules.Add(current);
                    current = next;
                }
            }
            mergedSchedules.Add(current);
        }

        _logger.LogTrace("Finished schedule optimization. Output count: {count}", mergedSchedules.Count);
        return mergedSchedules;
    }

    private bool AreSchedulesMergeable(DtoChargingSchedule a, DtoChargingSchedule b)
    {
        // Check all power-relevant properties and identifiers
        return a.TargetMinPower == b.TargetMinPower &&
               a.TargetHomeBatteryPower == b.TargetHomeBatteryPower &&
               a.EstimatedSolarPower == b.EstimatedSolarPower &&
               a.MaxPossiblePower == b.MaxPossiblePower &&
               a.CarId == b.CarId &&
               a.OcppChargingConnectorId == b.OcppChargingConnectorId;
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
            = _validFromToSplitter.SplitByBoundaries(newScheduleDummyList, existingSchedules,
                newChargingSchedule.ValidFrom, newChargingSchedule.ValidTo, true);
        _logger.LogTrace("After SplitByBoundaries: {newCount} new schedules, {existingCount} existing schedules", splittedNewChedules.Count, splittedExistingChargingSchedules.Count);

        foreach (var dtoChargingSchedule in splittedNewChedules)
        {
            _logger.LogTrace("Processing dtoChargingSchedule {@dtoChargingSchedule}", dtoChargingSchedule);
            var overlappingExistingChargingSchedule = splittedExistingChargingSchedules
                .FirstOrDefault(c => c.ValidFrom == dtoChargingSchedule.ValidFrom
                                     && c.ValidTo == dtoChargingSchedule.ValidTo);
            _logger.LogTrace("Overlapping schedule {hasOverlap}", overlappingExistingChargingSchedule == default ? "not found" : "found");
            if (overlappingExistingChargingSchedule == default)
            {
                var slotEnergy = CalculateChargedEnergy(dtoChargingSchedule.ValidTo - dtoChargingSchedule.ValidFrom, dtoChargingSchedule.EstimatedChargingPower);
                var energyToAdd = Math.Min(slotEnergy, maxEnergyToAdd - additionalScheduledEnergy);
                _logger.LogTrace("No overlap: slotEnergy={slotEnergy}, energyToAdd={energyToAdd}, additionalScheduledEnergy={additionalScheduledEnergy}, maxEnergyToAdd={maxEnergyToAdd}", slotEnergy, energyToAdd, additionalScheduledEnergy, maxEnergyToAdd);

                if (energyToAdd <= 0)
                {
                    _logger.LogTrace("Breaking because energyToAdd <= 0 (energyToAdd={energyToAdd})", energyToAdd);
                    break;
                }

                // If we need to add partial energy, adjust the power proportionally
                if (energyToAdd < slotEnergy)
                {
                    var hoursToReduce = (slotEnergy - energyToAdd) / (double)dtoChargingSchedule.EstimatedChargingPower;
                    _logger.LogTrace("Partial energy in new slot: reducing hours by {hoursToReduce}", hoursToReduce);
                    if (splittedExistingChargingSchedules.Any(s => s.ValidTo == dtoChargingSchedule.ValidFrom))
                    {
                        dtoChargingSchedule.ValidTo = dtoChargingSchedule.ValidTo.AddHours(-hoursToReduce);
                        _logger.LogTrace("Adjusted dtoChargingSchedule.ValidTo to {validTo}", dtoChargingSchedule.ValidTo);
                    }
                    else
                    {
                        dtoChargingSchedule.ValidFrom = dtoChargingSchedule.ValidFrom.AddHours(hoursToReduce);
                        _logger.LogTrace("Adjusted dtoChargingSchedule.ValidFrom to {validFrom}", dtoChargingSchedule.ValidFrom);
                    }
                }

                splittedExistingChargingSchedules.Add(dtoChargingSchedule);
                additionalScheduledEnergy += energyToAdd;
                _logger.LogTrace("Added new schedule, additionalScheduledEnergy now {additionalScheduledEnergy}", additionalScheduledEnergy);
                continue;
            }

            var earlierEstimatedPower = overlappingExistingChargingSchedule.EstimatedChargingPower;
            _logger.LogTrace("Overlap: earlierEstimatedPower={earlierEstimatedPower}", earlierEstimatedPower);

            var newMinTargetPower = Math.Max(overlappingExistingChargingSchedule.TargetMinPower, dtoChargingSchedule.TargetMinPower);
            var newEstimatedSolarPower = Math.Max(overlappingExistingChargingSchedule.EstimatedSolarPower, dtoChargingSchedule.EstimatedSolarPower);
            int? newTargetHomeBatteryPower = null;
            if (dtoChargingSchedule.TargetHomeBatteryPower.HasValue || overlappingExistingChargingSchedule.TargetHomeBatteryPower.HasValue)
            {
                newTargetHomeBatteryPower = Math.Max(overlappingExistingChargingSchedule.TargetHomeBatteryPower ?? 0,
                    dtoChargingSchedule.TargetHomeBatteryPower ?? 0);
            }
            _logger.LogTrace("Calculated merged targets: newMinTargetPower={newMinTargetPower}, newEstimatedSolarPower={newEstimatedSolarPower}, newTargetHomeBatteryPower={newTargetHomeBatteryPower}", newMinTargetPower, newEstimatedSolarPower, newTargetHomeBatteryPower);


            overlappingExistingChargingSchedule.TargetMinPower = newMinTargetPower;
            overlappingExistingChargingSchedule.EstimatedSolarPower = newEstimatedSolarPower;
            overlappingExistingChargingSchedule.TargetHomeBatteryPower = newTargetHomeBatteryPower;
            overlappingExistingChargingSchedule.ScheduleReasons.UnionWith(dtoChargingSchedule.ScheduleReasons);

        var addedPower = overlappingExistingChargingSchedule.EstimatedChargingPower - earlierEstimatedPower;
            var slotAddedEnergy = CalculateChargedEnergy(dtoChargingSchedule.ValidTo - dtoChargingSchedule.ValidFrom, addedPower);
            var energyToAdd1 = Math.Min(slotAddedEnergy, maxEnergyToAdd - additionalScheduledEnergy);
            _logger.LogTrace("Overlap energy: addedPower={addedPower}, slotAddedEnergy={slotAddedEnergy}, energyToAdd1={energyToAdd1}, additionalScheduledEnergy={additionalScheduledEnergy}, maxEnergyToAdd={maxEnergyToAdd}", addedPower, slotAddedEnergy, energyToAdd1, additionalScheduledEnergy, maxEnergyToAdd);

            if (energyToAdd1 < slotAddedEnergy && energyToAdd1 > 0)
            {
                var hoursToReduce = (slotAddedEnergy - energyToAdd1) / (double)dtoChargingSchedule.EstimatedChargingPower;
                _logger.LogTrace("Partial overlap energy: reducing hours by {hoursToReduce}", hoursToReduce);
                if (splittedExistingChargingSchedules.Any(s => s.ValidTo == dtoChargingSchedule.ValidFrom))
                {
                    overlappingExistingChargingSchedule.ValidTo = dtoChargingSchedule.ValidTo.AddHours(-hoursToReduce);
                    _logger.LogTrace("Adjusted overlappingExistingChargingSchedule.ValidTo to {validTo}", overlappingExistingChargingSchedule.ValidTo);
                }
                else
                {
                    overlappingExistingChargingSchedule.ValidFrom = dtoChargingSchedule.ValidFrom.AddHours(hoursToReduce);
                    _logger.LogTrace("Adjusted overlappingExistingChargingSchedule.ValidFrom to {validFrom}", overlappingExistingChargingSchedule.ValidFrom);
                }
                // Need to reduce the power increase to stay within limit
                var timeSpan = dtoChargingSchedule.ValidTo - dtoChargingSchedule.ValidFrom;
                var limitedAddedPower = (int)(energyToAdd1 / (timeSpan.TotalHours));
                overlappingExistingChargingSchedule.TargetMinPower = earlierEstimatedPower + limitedAddedPower;
                _logger.LogTrace("Limited added power: timeSpan={timeSpan}, limitedAddedPower={limitedAddedPower}, new TargetMinPower={targetMinPower}", timeSpan, limitedAddedPower, overlappingExistingChargingSchedule.TargetMinPower);
            }

            additionalScheduledEnergy += energyToAdd1;
            _logger.LogTrace("After overlap handling, additionalScheduledEnergy={additionalScheduledEnergy}", additionalScheduledEnergy);

            if (additionalScheduledEnergy >= maxEnergyToAdd)
            {
                _logger.LogTrace("Breaking because additionalScheduledEnergy >= maxEnergyToAdd ({additionalScheduledEnergy} >= {maxEnergyToAdd})", additionalScheduledEnergy, maxEnergyToAdd);
                break; // Reached the limit
            }
        }
        _logger.LogTrace("Returning from AddChargingSchedule with {scheduleCount} schedules and {additionalScheduledEnergy} additional energy", splittedExistingChargingSchedules.Count, additionalScheduledEnergy);
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
        _logger.LogTrace("{methdod}({carId}, {connectorId})", nameof(GetChargingScheduleRelevantData), carId, chargingConnectorId);
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
        _logger.LogTrace("CarPhases: {carPhases}", carPhases);
        _logger.LogTrace("Calculate max phases");
        var maxPhases = CalculateMaxValue(connectorData?.ConnectedPhasesCount, carPhases);
        _logger.LogTrace("Calculate max current");
        var maxCurrent = CalculateMaxValue(connectorData?.MaxCurrent, carData?.MaximumAmpere);
        _logger.LogTrace("Calculate min phases");
        var minPhases = CalculateMinValue(connectorData?.ConnectedPhasesCount, carPhases);
        _logger.LogTrace("Calculate min current");
        var minCurrent = CalculateMinValue(connectorData?.MinCurrent, carData?.MinimumAmpere);

        _logger.LogTrace("Result: usableEnergy={usableEnergy}, carSoc={carSoc}, maxPhases={maxPhases}, maxCurrent={maxCurrent}, minPhases={minPhases}, minCurrent={minCurrent}",
            carData?.UsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent);
        return (carData?.UsableEnergy, carSoC, maxPhases, maxCurrent, minPhases, minCurrent);
    }

    private int? CalculateMaxValue(int? connectorValue, int? carValue)
    {
        // If one is null, the HasValue check and Math.Max/Min are the cleanest approach.

        if (connectorValue.HasValue && carValue.HasValue)
        {
            // Both have values, return the greater one.
            return Math.Max(connectorValue.Value, carValue.Value);
        }

        if (connectorValue.HasValue)
        {
            // Only connector has value.
            return connectorValue;
        }

        // Only carValue has value, or both are null.
        return carValue;
    }

    private int? CalculateMinValue(int? connectorValue, int? carValue)
    {
        if (connectorValue.HasValue && carValue.HasValue)
        {
            // Both have values, return the smaller one.
            return Math.Min(connectorValue.Value, carValue.Value);
        }

        if (connectorValue.HasValue)
        {
            // Only connector has value.
            return connectorValue;
        }

        // Only carValue has value, or both are null.
        return carValue;
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
        _logger.LogTrace("{method}({phases}, {current}, {voltage})", nameof(GetPowerAtPhasesAndCurrent), phases, current, voltage);
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
