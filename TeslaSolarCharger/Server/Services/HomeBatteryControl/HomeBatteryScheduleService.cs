using TeslaSolarCharger.Server.Dtos.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.HomeBatteryControl;

/// <summary>
/// Plans time windows in which the home battery should hold its charge or charge from the grid so overall energy
/// costs are minimized:
/// <list type="bullet">
/// <item>While a car is intentionally charged from the grid, the battery is held so it does not discharge into the car.</item>
/// <item>When the battery would not last until solar self sufficiency, the house runs on grid (hold) during hours that
/// are cheaper than energy from the battery, and the battery is charged from the grid during the cheapest hours if
/// holds do not close the gap.</item>
/// </list>
/// </summary>
public class HomeBatteryScheduleService : IHomeBatteryScheduleService
{
    private readonly ILogger<HomeBatteryScheduleService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ISettings _settings;
    private readonly ITscOnlyChargingCostService _tscOnlyChargingCostService;
    private readonly IHomeBatteryEnergyCalculator _homeBatteryEnergyCalculator;
    private readonly IConstants _constants;

    public HomeBatteryScheduleService(ILogger<HomeBatteryScheduleService> logger,
        IConfigurationWrapper configurationWrapper,
        ISettings settings,
        ITscOnlyChargingCostService tscOnlyChargingCostService,
        IHomeBatteryEnergyCalculator homeBatteryEnergyCalculator,
        IConstants constants)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _settings = settings;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
        _homeBatteryEnergyCalculator = homeBatteryEnergyCalculator;
        _constants = constants;
    }

    public async Task PlanScheduleWindows(DateTimeOffset currentDate, List<DtoChargingSchedule> chargingSchedules, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({currentDate}, {scheduleCount} schedules)", nameof(PlanScheduleWindows), currentDate, chargingSchedules.Count);
        if (!_configurationWrapper.GridPriceBasedHomeBatteryControl())
        {
            ClearWindows();
            return;
        }
        var currentSoc = _settings.HomeBatterySoc;
        var usableEnergy = _configurationWrapper.HomeBatteryUsableEnergy();
        var holdTarget = _settings.HomeBatteryHoldTarget;
        var chargeTarget = _settings.HomeBatteryChargeTarget;
        if (currentSoc == default || usableEnergy == default || holdTarget == default || chargeTarget == default)
        {
            _logger.LogDebug("Can not plan home battery schedule windows: SoC, usable energy or SoC targets are unknown.");
            ClearWindows();
            return;
        }
        var maxTargetAge = TimeSpan.FromMinutes(_constants.HomeBatteryMinSocRefreshIntervalMinutes * 3);
        if (currentDate - holdTarget.CalculatedAt > maxTargetAge || currentDate - chargeTarget.CalculatedAt > maxTargetAge)
        {
            _logger.LogWarning("Can not plan home battery schedule windows: SoC targets are stale (hold: {holdCalculatedAt}, charge: {chargeCalculatedAt}).",
                holdTarget.CalculatedAt, chargeTarget.CalculatedAt);
            ClearWindows();
            return;
        }
        var surplusPrediction = await _homeBatteryEnergyCalculator.GetSurplusPrediction(currentDate, cancellationToken).ConfigureAwait(false);
        if (surplusPrediction == default)
        {
            _logger.LogDebug("Can not plan home battery schedule windows: no surplus prediction available.");
            ClearWindows();
            return;
        }
        var gridPrices = await _tscOnlyChargingCostService.GetPricesInTimeSpan(currentDate, surplusPrediction.SelfSufficiencyTime).ConfigureAwait(false);

        var input = new HomeBatteryPlanningInput
        {
            CurrentDate = currentDate,
            CurrentSocPercent = currentSoc.Value,
            UsableEnergyWh = usableEnergy.Value,
            MaxChargingPowerW = _configurationWrapper.HomeBatteryChargingPower(),
            HoldTargetSocPercent = holdTarget.RequiredInitialSocPercent,
            ChargeTargetSocPercent = chargeTarget.RequiredInitialSocPercent,
            SelfSufficiencyTime = surplusPrediction.SelfSufficiencyTime,
            UsageCostsPerKwh = _configurationWrapper.HomeBatteryUsageCostsPerKwh(),
            GridPrices = gridPrices,
            SurplusPerSlice = surplusPrediction.SurplusPerSlice,
            ChargingSchedules = chargingSchedules,
        };
        var windows = CalculateWindows(input);
        _logger.LogTrace("Planned home battery schedule windows: {@windows}", windows);
        _settings.HomeBatteryScheduleWindows = new(windows);
    }

    private void ClearWindows()
    {
        if (!_settings.HomeBatteryScheduleWindows.IsEmpty)
        {
            _settings.HomeBatteryScheduleWindows = new();
        }
    }

    /// <summary>
    /// Pure planning core. Deterministic and side effect free so it can be tested without mocks.
    /// Known simplifications (windows are replanned every few seconds, SoC guards limit the impact of wrong plans):
    /// grid charge windows are not required to lie before the deficit hours they cover, and battery grid charging is
    /// not counted against combined grid current limits together with car charging.
    /// </summary>
    internal static List<DtoHomeBatteryScheduleWindow> CalculateWindows(HomeBatteryPlanningInput input)
    {
        var carHoldIntervals = GetCarHoldIntervals(input);
        var slices = BuildPlanningSlices(input);

        var carHoldPreservedWh = slices.Where(s => s.CarGridChargeActive && s.NetEnergyWh < 0).Sum(s => -s.NetEnergyWh);
        var holdDeficitWh = CalculateDeficitWh(input.HoldTargetSocPercent, input, carHoldPreservedWh);
        var heldSlices = SelectHoldSlices(slices, holdDeficitWh, input);
        var economicHoldPreservedWh = heldSlices.Sum(s => -s.NetEnergyWh);
        var chargeDeficitWh = CalculateDeficitWh(input.ChargeTargetSocPercent, input, carHoldPreservedWh + economicHoldPreservedWh);
        var chargedEnergyBySlice = SelectChargeSlices(slices, heldSlices, chargeDeficitWh, input);

        var windows = BuildCarHoldWindows(carHoldIntervals, slices, input);
        windows.AddRange(BuildEconomicWindows(slices, heldSlices, chargedEnergyBySlice, input));
        return windows.OrderBy(w => w.ValidFrom).ToList();
    }

    private static int CalculateDeficitWh(int targetSocPercent, HomeBatteryPlanningInput input, int alreadyPreservedWh)
    {
        var deficit = (targetSocPercent - input.CurrentSocPercent) * input.UsableEnergyWh / 100 - alreadyPreservedWh;
        return Math.Max(0, deficit);
    }

    /// <summary>
    /// Selects the discharge slices the house should run on grid (hold) instead of draining the battery: the cheapest
    /// slices first, as long as they are cheaper than energy from the battery (cheapest chargeable price + usage
    /// costs) and cheaper than the most expensive slice the preserved energy will cover instead.
    /// </summary>
    private static HashSet<PlanningSlice> SelectHoldSlices(List<PlanningSlice> slices, int holdDeficitWh, HomeBatteryPlanningInput input)
    {
        var heldSlices = new HashSet<PlanningSlice>();
        if (holdDeficitWh <= 0)
        {
            return heldSlices;
        }
        var dischargeSlices = slices
            .Where(s => s.NetEnergyWh < 0 && !s.CarGridChargeActive && !s.DeliberateBatteryDischargeActive)
            .ToList();
        var batteryEnergyCostPerKwh = GetBatteryEnergyCostPerKwh(slices, input);
        var remainingDeficitWh = holdDeficitWh;
        foreach (var slice in dischargeSlices.OrderBy(s => s.GridPricePerKwh).ThenBy(s => s.From))
        {
            if (remainingDeficitWh <= 0)
            {
                break;
            }
            if (slice.GridPricePerKwh >= batteryEnergyCostPerKwh)
            {
                //Slices are ordered by price, so no cheaper slice follows. Covering more expensive times is up to grid charging.
                break;
            }
            var maxUncoveredPrice = dischargeSlices
                .Where(s => !heldSlices.Contains(s) && s != slice)
                .Select(s => s.GridPricePerKwh)
                .DefaultIfEmpty(decimal.MinValue)
                .Max();
            if (slice.GridPricePerKwh >= maxUncoveredPrice)
            {
                //Holding is only beneficial if the preserved energy covers a more expensive time, e.g. with flat prices holding never helps.
                break;
            }
            heldSlices.Add(slice);
            remainingDeficitWh -= -slice.NetEnergyWh;
        }
        return heldSlices;
    }

    /// <summary>
    /// Plans grid charge energy on the cheapest slices as long as the bought energy (price + usage costs) is cheaper
    /// than the most expensive unheld discharge slice it will cover.
    /// </summary>
    private static Dictionary<PlanningSlice, int> SelectChargeSlices(List<PlanningSlice> slices, HashSet<PlanningSlice> heldSlices,
        int chargeDeficitWh, HomeBatteryPlanningInput input)
    {
        var chargedEnergyBySlice = new Dictionary<PlanningSlice, int>();
        if (chargeDeficitWh <= 0 || !(input.MaxChargingPowerW > 0))
        {
            return chargedEnergyBySlice;
        }
        //Highest prices first: grid charged energy replaces the most expensive uncovered consumption.
        var uncoveredDemand = slices
            .Where(s => s.NetEnergyWh < 0 && !s.CarGridChargeActive && !s.DeliberateBatteryDischargeActive && !heldSlices.Contains(s))
            .OrderByDescending(s => s.GridPricePerKwh)
            .Select(s => new SliceDemand(s.GridPricePerKwh, -s.NetEnergyWh))
            .ToList();
        var chargeCandidates = slices
            .Where(s => s.NetEnergyWh <= 0 && !s.DeliberateBatteryDischargeActive)
            .OrderBy(s => s.GridPricePerKwh)
            .ThenBy(s => s.From);
        var remainingDeficitWh = chargeDeficitWh;
        var noFurtherMatchPossible = false;
        foreach (var chargeSlice in chargeCandidates)
        {
            if (remainingDeficitWh <= 0 || noFurtherMatchPossible)
            {
                break;
            }
            var sliceCapacityWh = (int)(input.MaxChargingPowerW.Value * chargeSlice.Duration.TotalHours);
            var chargedWh = 0;
            while (sliceCapacityWh > 0 && remainingDeficitWh > 0 && uncoveredDemand.Count > 0)
            {
                var mostExpensiveDemand = uncoveredDemand[0];
                if (chargeSlice.GridPricePerKwh + input.UsageCostsPerKwh >= mostExpensiveDemand.GridPricePerKwh)
                {
                    //Charge slices are ordered by price and remaining demand is cheaper, so no further match is possible at all.
                    noFurtherMatchPossible = true;
                    break;
                }
                var energyWh = Math.Min(Math.Min(sliceCapacityWh, remainingDeficitWh), mostExpensiveDemand.RemainingEnergyWh);
                chargedWh += energyWh;
                sliceCapacityWh -= energyWh;
                remainingDeficitWh -= energyWh;
                mostExpensiveDemand.RemainingEnergyWh -= energyWh;
                if (mostExpensiveDemand.RemainingEnergyWh <= 0)
                {
                    uncoveredDemand.RemoveAt(0);
                }
            }
            if (chargedWh > 0)
            {
                chargedEnergyBySlice[chargeSlice] = chargedWh;
            }
        }
        return chargedEnergyBySlice;
    }

    private static decimal GetBatteryEnergyCostPerKwh(List<PlanningSlice> slices, HomeBatteryPlanningInput input)
    {
        if (!(input.MaxChargingPowerW > 0))
        {
            //Without the ability to charge from grid, energy in the battery can not be replaced, so holds are only
            //limited by the deficit and the covered prices.
            return decimal.MaxValue;
        }
        var chargeableSlices = slices.Where(s => s.NetEnergyWh <= 0 && !s.DeliberateBatteryDischargeActive).ToList();
        if (chargeableSlices.Count == 0)
        {
            return decimal.MaxValue;
        }
        return chargeableSlices.Min(s => s.GridPricePerKwh) + input.UsageCostsPerKwh;
    }

    private static List<DtoHomeBatteryScheduleWindow> BuildCarHoldWindows(List<TimeInterval> carHoldIntervals,
        List<PlanningSlice> slices, HomeBatteryPlanningInput input)
    {
        var windows = new List<DtoHomeBatteryScheduleWindow>();
        foreach (var interval in carHoldIntervals)
        {
            var overlappingSlices = slices.Where(s => s.From < interval.To && s.To > interval.From).ToList();
            windows.Add(new DtoHomeBatteryScheduleWindow
            {
                ValidFrom = interval.From,
                ValidTo = interval.To,
                Mode = HomeBatteryMode.Hold,
                Reason = HomeBatteryScheduleWindowReason.CarGridCharging,
                OnlyWhileSocAtOrBelowPercent = input.HoldTargetSocPercent,
                PlannedEnergyWh = overlappingSlices.Where(s => s.NetEnergyWh < 0).Sum(s => -s.NetEnergyWh),
                GridPricePerKwh = GetDurationWeightedPrice(overlappingSlices),
            });
        }
        return windows;
    }

    private static List<DtoHomeBatteryScheduleWindow> BuildEconomicWindows(List<PlanningSlice> slices,
        HashSet<PlanningSlice> heldSlices, Dictionary<PlanningSlice, int> chargedEnergyBySlice, HomeBatteryPlanningInput input)
    {
        var windows = new List<DtoHomeBatteryScheduleWindow>();
        DtoHomeBatteryScheduleWindow? currentWindow = null;
        foreach (var slice in slices.OrderBy(s => s.From))
        {
            var window = CreateEconomicWindow(slice, heldSlices, chargedEnergyBySlice, input);
            if (window == default)
            {
                currentWindow = null;
                continue;
            }
            if (currentWindow != default
                && currentWindow.ValidTo == window.ValidFrom
                && currentWindow.Mode == window.Mode
                && currentWindow.Reason == window.Reason)
            {
                //Merge adjacent slices with the same mode into a single window. The price is kept duration weighted.
                var currentDuration = (decimal)(currentWindow.ValidTo - currentWindow.ValidFrom).TotalHours;
                var addedDuration = (decimal)(window.ValidTo - window.ValidFrom).TotalHours;
                currentWindow.GridPricePerKwh = (currentWindow.GridPricePerKwh * currentDuration + window.GridPricePerKwh * addedDuration)
                                                / (currentDuration + addedDuration);
                currentWindow.ValidTo = window.ValidTo;
                currentWindow.PlannedEnergyWh += window.PlannedEnergyWh;
                continue;
            }
            windows.Add(window);
            currentWindow = window;
        }
        return windows;
    }

    private static DtoHomeBatteryScheduleWindow? CreateEconomicWindow(PlanningSlice slice, HashSet<PlanningSlice> heldSlices,
        Dictionary<PlanningSlice, int> chargedEnergyBySlice, HomeBatteryPlanningInput input)
    {
        if (chargedEnergyBySlice.TryGetValue(slice, out var chargedEnergyWh))
        {
            return new DtoHomeBatteryScheduleWindow
            {
                ValidFrom = slice.From,
                ValidTo = slice.To,
                Mode = HomeBatteryMode.Charge,
                Reason = HomeBatteryScheduleWindowReason.GridChargeForDeficit,
                TargetSocPercent = input.ChargeTargetSocPercent,
                PlannedEnergyWh = chargedEnergyWh,
                GridPricePerKwh = slice.GridPricePerKwh,
            };
        }
        if (heldSlices.Contains(slice))
        {
            return new DtoHomeBatteryScheduleWindow
            {
                ValidFrom = slice.From,
                ValidTo = slice.To,
                Mode = HomeBatteryMode.Hold,
                Reason = HomeBatteryScheduleWindowReason.PreserveForDeficit,
                OnlyWhileSocAtOrBelowPercent = input.HoldTargetSocPercent,
                PlannedEnergyWh = -slice.NetEnergyWh,
                GridPricePerKwh = slice.GridPricePerKwh,
            };
        }
        return null;
    }

    private static decimal GetDurationWeightedPrice(List<PlanningSlice> slices)
    {
        var totalHours = (decimal)slices.Sum(s => s.Duration.TotalHours);
        if (totalHours == 0)
        {
            return 0;
        }
        return slices.Sum(s => s.GridPricePerKwh * (decimal)s.Duration.TotalHours) / totalHours;
    }

    /// <summary>
    /// Time ranges in which a car is intentionally charged from the grid, so the battery should be held. Ranges where
    /// the battery is intentionally discharged into a car are excluded.
    /// </summary>
    private static List<TimeInterval> GetCarHoldIntervals(HomeBatteryPlanningInput input)
    {
        var gridChargeIntervals = MergeIntervals(input.ChargingSchedules
            .Where(s => s.ValidTo > input.CurrentDate && s.TargetMinPower > 0 && !(s.TargetHomeBatteryPower > 0))
            .Select(s => new TimeInterval(Max(s.ValidFrom, input.CurrentDate), s.ValidTo)));
        var deliberateDischargeIntervals = GetDeliberateDischargeIntervals(input);
        return SubtractIntervals(gridChargeIntervals, deliberateDischargeIntervals);
    }

    private static List<TimeInterval> GetDeliberateDischargeIntervals(HomeBatteryPlanningInput input)
    {
        return MergeIntervals(input.ChargingSchedules
            .Where(s => s.ValidTo > input.CurrentDate && s.TargetHomeBatteryPower > 0)
            .Select(s => new TimeInterval(Max(s.ValidFrom, input.CurrentDate), s.ValidTo)));
    }

    /// <summary>
    /// Splits the planning range (now until self sufficiency, clipped to known prices) into slices at every price,
    /// surplus hour and charging schedule boundary, so each slice has a single price, net energy and car state.
    /// </summary>
    private static List<PlanningSlice> BuildPlanningSlices(HomeBatteryPlanningInput input)
    {
        var result = new List<PlanningSlice>();
        if (input.GridPrices.Count == 0)
        {
            return result;
        }
        var planEnd = Min(input.SelfSufficiencyTime, input.GridPrices.Max(p => p.ValidTo));
        if (planEnd <= input.CurrentDate)
        {
            return result;
        }
        var boundaries = new SortedSet<DateTimeOffset> { input.CurrentDate, planEnd, };
        foreach (var price in input.GridPrices)
        {
            AddBoundaryIfInRange(boundaries, price.ValidFrom, input.CurrentDate, planEnd);
            AddBoundaryIfInRange(boundaries, price.ValidTo, input.CurrentDate, planEnd);
        }
        foreach (var sliceStart in input.SurplusPerSlice.Keys)
        {
            AddBoundaryIfInRange(boundaries, sliceStart, input.CurrentDate, planEnd);
        }
        foreach (var schedule in input.ChargingSchedules)
        {
            AddBoundaryIfInRange(boundaries, schedule.ValidFrom, input.CurrentDate, planEnd);
            AddBoundaryIfInRange(boundaries, schedule.ValidTo, input.CurrentDate, planEnd);
        }
        var carHoldIntervals = GetCarHoldIntervals(input);
        var deliberateDischargeIntervals = GetDeliberateDischargeIntervals(input);
        var boundaryList = boundaries.ToList();
        for (var i = 0; i < boundaryList.Count - 1; i++)
        {
            var from = boundaryList[i];
            var to = boundaryList[i + 1];
            var price = input.GridPrices.FirstOrDefault(p => p.ValidFrom <= from && p.ValidTo > from);
            if (price == default)
            {
                //Without a known price no economic decision is possible for this slice.
                continue;
            }
            result.Add(new PlanningSlice
            {
                From = from,
                To = to,
                GridPricePerKwh = price.GridPrice,
                NetEnergyWh = GetNetEnergyOfSlice(input.SurplusPerSlice, from, to),
                CarGridChargeActive = carHoldIntervals.Any(c => c.From < to && c.To > from),
                DeliberateBatteryDischargeActive = deliberateDischargeIntervals.Any(d => d.From < to && d.To > from),
            });
        }
        return result;
    }

    /// <summary>
    /// Gets the predicted net energy for a slice from the hourly surplus prediction, prorated by the slice duration.
    /// For the already started hour the prediction of the following hour is used as an approximation.
    /// </summary>
    private static int GetNetEnergyOfSlice(Dictionary<DateTimeOffset, int> surplusPerSlice, DateTimeOffset from, DateTimeOffset to)
    {
        var surplusInterval = TimeSpan.FromHours(1);
        var hourStart = new DateTimeOffset(from.Year, from.Month, from.Day, from.Hour, 0, 0, from.Offset);
        if (!surplusPerSlice.TryGetValue(hourStart, out var energyPerHour)
            && !surplusPerSlice.TryGetValue(hourStart.Add(surplusInterval), out energyPerHour))
        {
            return 0;
        }
        return (int)(energyPerHour * ((to - from) / surplusInterval));
    }

    private static void AddBoundaryIfInRange(SortedSet<DateTimeOffset> boundaries, DateTimeOffset value, DateTimeOffset min, DateTimeOffset max)
    {
        if (value > min && value < max)
        {
            boundaries.Add(value);
        }
    }

    private static List<TimeInterval> MergeIntervals(IEnumerable<TimeInterval> intervals)
    {
        var result = new List<TimeInterval>();
        foreach (var interval in intervals.Where(i => i.To > i.From).OrderBy(i => i.From))
        {
            var last = result.LastOrDefault();
            if (last != default && interval.From <= last.To)
            {
                last.To = Max(last.To, interval.To);
                continue;
            }
            result.Add(new TimeInterval(interval.From, interval.To));
        }
        return result;
    }

    private static List<TimeInterval> SubtractIntervals(List<TimeInterval> minuends, List<TimeInterval> subtrahends)
    {
        var result = new List<TimeInterval>();
        foreach (var minuend in minuends)
        {
            var remainingFrom = minuend.From;
            foreach (var subtrahend in subtrahends.Where(s => s.From < minuend.To && s.To > minuend.From).OrderBy(s => s.From))
            {
                if (subtrahend.From > remainingFrom)
                {
                    result.Add(new TimeInterval(remainingFrom, subtrahend.From));
                }
                remainingFrom = Max(remainingFrom, subtrahend.To);
            }
            if (remainingFrom < minuend.To)
            {
                result.Add(new TimeInterval(remainingFrom, minuend.To));
            }
        }
        return result;
    }

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;
    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;

    private sealed class TimeInterval
    {
        public TimeInterval(DateTimeOffset from, DateTimeOffset to)
        {
            From = from;
            To = to;
        }

        public DateTimeOffset From { get; }
        public DateTimeOffset To { get; set; }
    }

    private sealed class SliceDemand
    {
        public SliceDemand(decimal gridPricePerKwh, int remainingEnergyWh)
        {
            GridPricePerKwh = gridPricePerKwh;
            RemainingEnergyWh = remainingEnergyWh;
        }

        public decimal GridPricePerKwh { get; }
        public int RemainingEnergyWh { get; set; }
    }

    private sealed class PlanningSlice
    {
        public DateTimeOffset From { get; init; }
        public DateTimeOffset To { get; init; }
        public decimal GridPricePerKwh { get; init; }
        /// <summary>Predicted surplus in Wh, negative when the house draws energy.</summary>
        public int NetEnergyWh { get; init; }
        public bool CarGridChargeActive { get; init; }
        public bool DeliberateBatteryDischargeActive { get; init; }
        public TimeSpan Duration => To - From;
    }
}
