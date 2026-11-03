using TeslaSolarCharger.Server.Dtos.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.HomeBatteryControl;

/// <summary>
/// Plans time windows in which the home battery should hold its charge or charge from the grid so overall energy
/// costs are minimized. House consumption and the grid energy of planned car charging are treated as one demand per
/// time slice: the most expensive demand is covered by the battery first, either with energy already stored or with
/// energy charged from the grid in a cheaper, earlier slice, as long as that is cheaper including the battery usage
/// costs and the battery stays between the min SoC and the max charge SoC. Car charging schedules are not changed.
/// </summary>
public class HomeBatteryScheduleService : IHomeBatteryScheduleService
{
    private readonly ILogger<HomeBatteryScheduleService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ISettings _settings;
    private readonly ITscOnlyChargingCostService _tscOnlyChargingCostService;
    private readonly IHomeBatteryEnergyCalculator _homeBatteryEnergyCalculator;

    public HomeBatteryScheduleService(ILogger<HomeBatteryScheduleService> logger,
        IConfigurationWrapper configurationWrapper,
        ISettings settings,
        ITscOnlyChargingCostService tscOnlyChargingCostService,
        IHomeBatteryEnergyCalculator homeBatteryEnergyCalculator)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _settings = settings;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
        _homeBatteryEnergyCalculator = homeBatteryEnergyCalculator;
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
        var chargingEnergy = _configurationWrapper.HomeBatteryChargingEnergy();
        var dischargingEnergy = _configurationWrapper.HomeBatteryDischargingEnergy();
        if (currentSoc == default || chargingEnergy == default || dischargingEnergy == default)
        {
            _logger.LogDebug("Can not plan home battery schedule windows: SoC or usable energy is unknown.");
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
            ChargingEnergyWh = chargingEnergy.Value,
            DischargingEnergyWh = dischargingEnergy.Value,
            MaxChargingPowerW = _configurationWrapper.HomeBatteryChargingPower(),
            MaxDischargingPowerW = _configurationWrapper.HomeBatteryDischargingPower(),
            MinSocPercent = GetMinSocPercent(),
            MaxChargeSocPercent = _configurationWrapper.HomeBatteryMaxChargeSoc(),
            ChargeBufferPercent = _configurationWrapper.ChargeHomeBatterySocBufferInPercent(),
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

    private int GetMinSocPercent()
    {
        //With dynamic min SoC the configured min SoC is overwritten with the SoC needed until sunrise, which would forbid
        //any planned discharge at night, so its lower limit is used instead.
        return _configurationWrapper.DynamicHomeBatteryMinSoc()
            ? _configurationWrapper.HomeBatteryMinDynamicMinSoc()
            : _configurationWrapper.HomeBatteryMinSoc() ?? 0;
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
    /// Known simplifications (windows are replanned every few seconds): car charging is not slowed down to the battery
    /// discharge power, battery grid charging is not counted against grid current limits together with car charging and
    /// the planning ends at solar self sufficiency.
    /// </summary>
    internal static List<DtoHomeBatteryScheduleWindow> CalculateWindows(HomeBatteryPlanningInput input)
    {
        var slices = BuildPlanningSlices(input);
        var battery = new BatteryLimits(input);
        var houseGridChargeBudgetWh = GetHouseGridChargeBudgetWh(slices, battery, input);
        var demandSlices = slices
            .Where(s => !s.DeliberateDischarge && s.DemandWh > 0)
            .OrderByDescending(s => s.GridPricePerKwh)
            .ThenBy(s => s.From)
            .ToList();
        foreach (var slice in demandSlices)
        {
            if (slice.ChargeWh > 0)
            {
                //The battery is charged during this slice, so the demand is covered by the grid.
                continue;
            }
            CoverFromStoredEnergy(slices, slice, battery, input);
            houseGridChargeBudgetWh = CoverFromGridCharge(slices, slice, battery, input, houseGridChargeBudgetWh);
        }
        return BuildWindows(slices, battery, input);
    }

    /// <summary>
    /// Energy grid charging may buy for house consumption: the predicted consumption plus the charge buffer minus the
    /// energy already stored above the min SoC. Car consumption is not limited by this budget.
    /// </summary>
    private static int GetHouseGridChargeBudgetWh(IEnumerable<PlanningSlice> slices, BatteryLimits battery, HomeBatteryPlanningInput input)
    {
        var houseDemandWh = slices.Where(s => !s.DeliberateDischarge).Sum(s => s.HouseDemandWh);
        var bufferWh = input.ChargeBufferPercent * input.DischargingEnergyWh / 100.0;
        var storedWh = Math.Max(0, battery.InitialContentWh - battery.MinContentWh);
        return (int)Math.Max(0, houseDemandWh + bufferWh - storedWh);
    }

    /// <summary>
    /// Covers the demand of the slice with energy already in the battery, as far as the battery does not drop below the
    /// min SoC in this or any later slice. House consumption is covered first as the house always draws first.
    /// </summary>
    private static void CoverFromStoredEnergy(List<PlanningSlice> slices, PlanningSlice slice, BatteryLimits battery,
        HomeBatteryPlanningInput input)
    {
        if (slice.GridPricePerKwh <= input.UsageCostsPerKwh)
        {
            return;
        }
        var contentAtEnd = SimulateContentAtEnd(slices, battery);
        var index = slices.IndexOf(slice);
        var availableWh = contentAtEnd.Skip(index).Min() - battery.MinContentWh;
        if (availableWh <= 0)
        {
            return;
        }
        var houseWh = (int)Math.Min(availableWh, slice.HouseDemandWh - slice.HouseCoveredWh);
        slice.HouseCoveredWh += houseWh;
        availableWh -= houseWh;
        slice.CarCoveredWh += (int)Math.Min(availableWh, slice.CarDemandWh - slice.CarCoveredWh);
    }

    /// <summary>
    /// Covers the remaining demand of the slice with energy charged from the grid in earlier slices, cheapest first, as
    /// long as the grid price plus the usage costs is below the price of the covered slice and the battery does not
    /// exceed the max charge SoC until the energy is used.
    /// </summary>
    /// <returns>The remaining house grid charge budget.</returns>
    private static int CoverFromGridCharge(List<PlanningSlice> slices, PlanningSlice slice, BatteryLimits battery,
        HomeBatteryPlanningInput input, int houseGridChargeBudgetWh)
    {
        if (!(input.MaxChargingPowerW > 0))
        {
            return houseGridChargeBudgetWh;
        }
        var index = slices.IndexOf(slice);
        var chargeCandidates = slices
            .Take(index)
            .Where(s => !s.DeliberateDischarge && s.CoveredWh == 0)
            .Where(s => s.GridPricePerKwh + input.UsageCostsPerKwh < slice.GridPricePerKwh)
            .OrderBy(s => s.GridPricePerKwh)
            .ThenBy(s => s.From)
            .ToList();
        foreach (var chargeSlice in chargeCandidates)
        {
            var carMissingWh = slice.CarDemandWh - slice.CarCoveredWh;
            var houseMissingWh = Math.Min(slice.HouseDemandWh - slice.HouseCoveredWh, houseGridChargeBudgetWh);
            var missingWh = carMissingWh + houseMissingWh;
            if (missingWh <= 0)
            {
                break;
            }
            var contentAtEnd = SimulateContentAtEnd(slices, battery);
            var chargeIndex = slices.IndexOf(chargeSlice);
            //The bought energy stays in the battery from the charge slice until it is used.
            var roomWh = battery.MaxChargeContentWh - contentAtEnd.Skip(chargeIndex).Take(index - chargeIndex).Max();
            var chargeCapacityWh = input.MaxChargingPowerW!.Value * chargeSlice.Duration.TotalHours / battery.GridWhPerStoredWh
                                   - chargeSlice.ChargeWh;
            var chargedWh = (int)Math.Min(missingWh, Math.Min(roomWh, chargeCapacityWh));
            if (chargedWh <= 0)
            {
                continue;
            }
            chargeSlice.ChargeWh += chargedWh;
            var carWh = Math.Min(chargedWh, carMissingWh);
            slice.CarCoveredWh += carWh;
            slice.HouseCoveredWh += chargedWh - carWh;
            houseGridChargeBudgetWh -= chargedWh - carWh;
        }
        return houseGridChargeBudgetWh;
    }

    /// <summary>
    /// Battery content in stored Wh at the end of each slice with the planned charging and discharging. Solar surplus
    /// charges the battery up to 100 %, deliberate discharging into cars stops at the min SoC.
    /// </summary>
    private static double[] SimulateContentAtEnd(List<PlanningSlice> slices, BatteryLimits battery)
    {
        var result = new double[slices.Count];
        var contentWh = battery.InitialContentWh;
        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            contentWh = Math.Min(battery.FullContentWh, contentWh + slice.SolarSurplusWh / battery.GridWhPerStoredWh + slice.ChargeWh);
            contentWh -= slice.CoveredWh;
            contentWh -= Math.Min(slice.DeliberateDischargeWh, Math.Max(0, contentWh - battery.MinContentWh));
            result[i] = contentWh;
        }
        return result;
    }

    /// <summary>
    /// Charge slices become charge windows. Slices with demand the battery should not cover become hold windows while
    /// the battery has energy that would otherwise be discharged. All other slices keep the vendor default behavior.
    /// </summary>
    private static List<DtoHomeBatteryScheduleWindow> BuildWindows(List<PlanningSlice> slices, BatteryLimits battery,
        HomeBatteryPlanningInput input)
    {
        var contentAtEnd = SimulateContentAtEnd(slices, battery);
        var windows = new List<DtoHomeBatteryScheduleWindow>();
        DtoHomeBatteryScheduleWindow? currentWindow = null;
        for (var i = 0; i < slices.Count; i++)
        {
            var contentAtStart = i == 0 ? battery.InitialContentWh : contentAtEnd[i - 1];
            var window = CreateWindow(slices[i], contentAtStart, contentAtEnd[i], battery, input);
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
                if (window.TargetSocPercent > currentWindow.TargetSocPercent)
                {
                    currentWindow.TargetSocPercent = window.TargetSocPercent;
                }
                continue;
            }
            windows.Add(window);
            currentWindow = window;
        }
        return windows;
    }

    private static DtoHomeBatteryScheduleWindow? CreateWindow(PlanningSlice slice, double contentAtStart, double contentAtEnd,
        BatteryLimits battery, HomeBatteryPlanningInput input)
    {
        if (slice.ChargeWh > 0)
        {
            return new DtoHomeBatteryScheduleWindow
            {
                ValidFrom = slice.From,
                ValidTo = slice.To,
                Mode = HomeBatteryMode.Charge,
                Reason = HomeBatteryScheduleWindowReason.GridChargeForDeficit,
                TargetSocPercent = Math.Min(input.MaxChargeSocPercent, (int)Math.Ceiling(contentAtEnd * 100 / input.DischargingEnergyWh)),
                PlannedEnergyWh = (int)Math.Round(slice.ChargeWh * battery.GridWhPerStoredWh),
                GridPricePerKwh = slice.GridPricePerKwh,
            };
        }
        //Less than 1 Wh above the min SoC is nothing worth holding.
        if (slice.DeliberateDischarge || slice.DemandWh <= 0 || slice.CoveredWh > 0 || contentAtStart < battery.MinContentWh + 1)
        {
            return null;
        }
        return new DtoHomeBatteryScheduleWindow
        {
            ValidFrom = slice.From,
            ValidTo = slice.To,
            Mode = HomeBatteryMode.Hold,
            Reason = slice.CarDemandWh > 0 ? HomeBatteryScheduleWindowReason.CarGridCharging : HomeBatteryScheduleWindowReason.PreserveForDeficit,
            PlannedEnergyWh = slice.DemandWh,
            GridPricePerKwh = slice.GridPricePerKwh,
        };
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
            result.Add(CreatePlanningSlice(input, from, to, price.GridPrice));
        }
        return result;
    }

    private static PlanningSlice CreatePlanningSlice(HomeBatteryPlanningInput input, DateTimeOffset from, DateTimeOffset to, decimal gridPrice)
    {
        var hours = (to - from).TotalHours;
        var netEnergyWh = GetNetEnergyOfSlice(input.SurplusPerSlice, from, to);
        var overlappingSchedules = input.ChargingSchedules.Where(s => s.ValidFrom < to && s.ValidTo > from).ToList();
        var deliberateDischargePower = overlappingSchedules.Where(s => s.TargetHomeBatteryPower > 0).Sum(s => s.TargetHomeBatteryPower!.Value);
        var carGridPower = overlappingSchedules.Where(s => s.TargetMinPower > 0 && !(s.TargetHomeBatteryPower > 0)).Sum(s => s.TargetMinPower);
        //The battery can only cover demand up to its discharge power, the house draws first.
        var maxDischargeWh = input.MaxDischargingPowerW > 0 ? (int)(input.MaxDischargingPowerW.Value * hours) : int.MaxValue;
        var houseDemandWh = Math.Min(Math.Max(0, -netEnergyWh), maxDischargeWh);
        var slice = new PlanningSlice
        {
            From = from,
            To = to,
            GridPricePerKwh = gridPrice,
            SolarSurplusWh = Math.Max(0, netEnergyWh),
        };
        if (deliberateDischargePower > 0)
        {
            //The battery is deliberately discharged into a car, so the slice is not planned but drains the battery.
            slice.DeliberateDischarge = true;
            slice.DeliberateDischargeWh = Math.Min(maxDischargeWh, houseDemandWh + (int)(deliberateDischargePower * hours));
            return slice;
        }
        slice.HouseDemandWh = houseDemandWh;
        slice.CarDemandWh = Math.Min((int)(carGridPower * hours), maxDischargeWh - houseDemandWh);
        return slice;
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

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;

    /// <summary>
    /// Battery content limits in stored Wh: the energy the battery delivers, so 100 % equals the discharging energy.
    /// </summary>
    private sealed class BatteryLimits
    {
        public BatteryLimits(HomeBatteryPlanningInput input)
        {
            FullContentWh = input.DischargingEnergyWh;
            InitialContentWh = input.CurrentSocPercent * FullContentWh / 100.0;
            MinContentWh = input.MinSocPercent * FullContentWh / 100.0;
            MaxChargeContentWh = input.MaxChargeSocPercent * FullContentWh / 100.0;
            GridWhPerStoredWh = (double)input.ChargingEnergyWh / input.DischargingEnergyWh;
        }

        public double FullContentWh { get; }
        public double InitialContentWh { get; }
        public double MinContentWh { get; }
        public double MaxChargeContentWh { get; }
        /// <summary>Grid energy needed to store 1 Wh the battery can deliver again (charging and discharging losses).</summary>
        public double GridWhPerStoredWh { get; }
    }

    private sealed class PlanningSlice
    {
        public DateTimeOffset From { get; init; }
        public DateTimeOffset To { get; init; }
        public decimal GridPricePerKwh { get; init; }
        /// <summary>Predicted solar energy exceeding the house consumption, charges the battery.</summary>
        public int SolarSurplusWh { get; init; }
        /// <summary>Predicted house consumption the battery could cover, limited by its discharge power.</summary>
        public int HouseDemandWh { get; set; }
        /// <summary>Planned car grid charging energy the battery could cover, limited by its remaining discharge power.</summary>
        public int CarDemandWh { get; set; }
        public bool DeliberateDischarge { get; set; }
        public int DeliberateDischargeWh { get; set; }
        public int HouseCoveredWh { get; set; }
        public int CarCoveredWh { get; set; }
        /// <summary>Stored Wh planned to be charged from the grid in this slice.</summary>
        public int ChargeWh { get; set; }
        public int DemandWh => HouseDemandWh + CarDemandWh;
        public int CoveredWh => HouseCoveredWh + CarCoveredWh;
        public TimeSpan Duration => To - From;
    }
}
