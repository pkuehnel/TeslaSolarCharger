using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class ChargeTimeCalculationService : IChargeTimeCalculationService
{
    private readonly ILogger<ChargeTimeCalculationService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly ISpotPriceService _spotPriceService;

    public ChargeTimeCalculationService(ILogger<ChargeTimeCalculationService> logger, IDateTimeProvider dateTimeProvider,
        ISettings settings, ITeslaSolarChargerContext teslaSolarChargerContext, ISpotPriceService spotPriceService)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _spotPriceService = spotPriceService;
    }

    public TimeSpan CalculateTimeToReachMinSocAtFullSpeedCharge(Car car)
    {
        _logger.LogTrace("{method}({carId})", nameof(CalculateTimeToReachMinSocAtFullSpeedCharge), car.Id);
        var socToCharge = (double)car.CarConfiguration.MinimumSoC - (car.CarState.SoC ?? 0);
        if (socToCharge < 1 || (socToCharge < 3 && (car.CarState.ChargerActualCurrent ?? 1) < 1))
        {
            return TimeSpan.Zero;
        }

        var energyToCharge = car.CarConfiguration.UsableEnergy * 1000 * (decimal)(socToCharge / 100.0);
        var numberOfPhases = car.CarState.ActualPhases;
        var maxChargingPower =
            car.CarConfiguration.MaximumAmpere * numberOfPhases
                                               //Use 230 instead of actual voltage because of 0 Volt if charging is stopped
                                               * 230;
        return TimeSpan.FromHours((double)(energyToCharge / maxChargingPower));
    }

    public void UpdateChargeTime(Car car)
    {
        car.CarState.ReachingMinSocAtFullSpeedCharge = _dateTimeProvider.Now() + CalculateTimeToReachMinSocAtFullSpeedCharge(car);
    }


    public async Task PlanChargeTimesForAllCars()
    {
        _logger.LogTrace("{method}()", nameof(PlanChargeTimesForAllCars));
        var carsToPlan = _settings.Cars.Where(c => c.CarConfiguration.ShouldBeManaged == true).ToList();
        foreach (var car in carsToPlan)
        {
            await UpdatePlannedChargingSlots(car).ConfigureAwait(false);
        }
    }

    public async Task UpdatePlannedChargingSlots(Car car)
    {
        _logger.LogTrace("{method}({carId}", nameof(UpdatePlannedChargingSlots), car.Id);
        var dateTimeOffSetNow = _dateTimeProvider.DateTimeOffSetNow();

        var plannedChargingSlots = await PlanChargingSlots(car, dateTimeOffSetNow).ConfigureAwait(false);
        ReplaceFirstChargingSlotStartTimeIfAlreadyActive(car, plannedChargingSlots, dateTimeOffSetNow);

        //ToDo: if no new planned charging slot and one chargingslot is active, do not remove it if min soc is not reached.
        car.CarState.PlannedChargingSlots = plannedChargingSlots;

    }

    private void ReplaceFirstChargingSlotStartTimeIfAlreadyActive(Car car, List<DtoChargingSlot> plannedChargingSlots,
        DateTimeOffset dateTimeOffSetNow)
    {
        _logger.LogTrace("{method}({carId}, {@plannedChargingSlots}, {dateTimeOffSetNow}", nameof(ReplaceFirstChargingSlotStartTimeIfAlreadyActive), car.Id, plannedChargingSlots, dateTimeOffSetNow);
        //If a planned charging session is faster than expected, only stop charging if charge end is more than 15 minutes earlier than expected.
        var maximumOffSetOfActiveChargingSession = TimeSpan.FromMinutes(15);

        var earliestPlannedChargingSession = plannedChargingSlots
            .Where(c => c.ChargeStart <= (dateTimeOffSetNow + maximumOffSetOfActiveChargingSession))
            .MinBy(c => c.ChargeStart);
        var activeChargingSession = car.CarState.PlannedChargingSlots.FirstOrDefault(c => c.IsActive);

        if (earliestPlannedChargingSession != default && activeChargingSession != default)
        {
            var chargingDuration = earliestPlannedChargingSession.ChargeDuration;
            activeChargingSession.ChargeEnd = dateTimeOffSetNow + chargingDuration;
            plannedChargingSlots.Remove(earliestPlannedChargingSession);
            plannedChargingSlots.Add(activeChargingSession);
        }
    }

    internal async Task<List<DtoChargingSlot>> PlanChargingSlots(Car car, DateTimeOffset dateTimeOffSetNow)
    {
        _logger.LogTrace("{method}({carId}, {dateTimeOffset}", nameof(PlanChargingSlots), car.Id, dateTimeOffSetNow);
        var plannedChargingSlots = new List<DtoChargingSlot>();
        var chargeDurationToMinSoc = CalculateTimeToReachMinSocAtFullSpeedCharge(car);
        var latestTimeToReachSoc =
            new DateTimeOffset(car.CarConfiguration.LatestTimeToReachSoC, TimeZoneInfo.Local.BaseUtcOffset);
        if (chargeDurationToMinSoc == TimeSpan.Zero && car.CarConfiguration.ChargeMode != ChargeMode.MaxPower)
        {
            //No charging is planned
        }
        else
        {
            if (car.CarConfiguration.ChargeMode is ChargeMode.PvOnly or ChargeMode.SpotPrice
                && !IsAbleToReachSocInTime(car, chargeDurationToMinSoc, dateTimeOffSetNow, latestTimeToReachSoc))
            {
                var plannedChargeSlot = GenerateChargingSlotFromNow(dateTimeOffSetNow, chargeDurationToMinSoc);
                plannedChargingSlots.Add(plannedChargeSlot);
                return plannedChargingSlots;
            }
            switch (car.CarConfiguration.ChargeMode)
            {
                case ChargeMode.PvAndMinSoc:
                    var plannedChargeSlot = GenerateChargingSlotFromNow(dateTimeOffSetNow, chargeDurationToMinSoc);
                    plannedChargingSlots.Add(plannedChargeSlot);
                    break;

                case ChargeMode.PvOnly:
                    var plannedChargingSlot = new DtoChargingSlot();
                    if (latestTimeToReachSoc > dateTimeOffSetNow)
                    {
                        plannedChargingSlot = new DtoChargingSlot()
                        {
                            ChargeEnd = latestTimeToReachSoc,
                            ChargeStart = latestTimeToReachSoc - chargeDurationToMinSoc,
                        };
                    }
                    plannedChargingSlots.Add(plannedChargingSlot);
                    break;

                case ChargeMode.MaxPower:
                    plannedChargingSlots.Add(new DtoChargingSlot()
                    {
                        ChargeStart = dateTimeOffSetNow,
                        ChargeEnd = DateTimeOffset.MaxValue,
                    });
                    break;

                case ChargeMode.SpotPrice:
                    //ToDo: Plan hours that are cheaper than solar price
                    var chargingSlots = await GenerateSpotPriceChargingSlots(car, chargeDurationToMinSoc, dateTimeOffSetNow, latestTimeToReachSoc).ConfigureAwait(false);
                    plannedChargingSlots.AddRange(chargingSlots);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return plannedChargingSlots.OrderBy(s => s.ChargeStart).ToList();
    }

    private static DtoChargingSlot GenerateChargingSlotFromNow(DateTimeOffset dateTimeOffSetNow,
        TimeSpan chargeDurationToMinSoc)
    {
        var plannedChargeSlot = new DtoChargingSlot()
        {
            ChargeStart = dateTimeOffSetNow,
            ChargeEnd = dateTimeOffSetNow + chargeDurationToMinSoc,
        };
        return plannedChargeSlot;
    }

    //ToDo: Add Unit Tests for this
    internal async Task<List<DtoChargingSlot>> GenerateSpotPriceChargingSlots(Car car, TimeSpan chargeDurationToMinSoc,
        DateTimeOffset dateTimeOffSetNow, DateTimeOffset latestTimeToReachSoc)
    {
        _logger.LogTrace("{method}({carId}, {chargeDurationToMinSoc}", nameof(GenerateSpotPriceChargingSlots), car.Id, chargeDurationToMinSoc);
        var chargingSlots = new List<DtoChargingSlot>();
        var chargePricesUntilLatestTimeToReachSocOrderedByPrice =
            await ChargePricesUntilLatestTimeToReachSocOrderedByPrice(dateTimeOffSetNow, latestTimeToReachSoc).ConfigureAwait(false);
        if (await IsLatestTimeToReachSocAfterLatestKnownChargePrice(car.Id).ConfigureAwait(false))
        {
            return chargingSlots;
        }

        var restTimeNeeded = chargeDurationToMinSoc;
        var chargingSlotsBeforeConcatenation = new List<DtoChargingSlot>();
        var minDate = dateTimeOffSetNow;
        foreach (var cheapestPrice in chargePricesUntilLatestTimeToReachSocOrderedByPrice)
        {
            var chargingSlot = GenerateChargingSlotBySpotPrice(cheapestPrice, minDate, latestTimeToReachSoc);
            restTimeNeeded -= chargingSlot.ChargeDuration;
            if (restTimeNeeded < TimeSpan.Zero)
            {
                chargingSlot.ChargeEnd = chargingSlot.ChargeEnd.Add(restTimeNeeded);
            }
            chargingSlotsBeforeConcatenation.Add(chargingSlot);
            if (restTimeNeeded < TimeSpan.Zero)
            {
                break;
            }
        }
        chargingSlots = ConcatenateChargeTimes(chargingSlotsBeforeConcatenation);
        return chargingSlots;
    }

    private bool IsAbleToReachSocInTime(Car car, TimeSpan chargeDurationToMinSoc,
        DateTimeOffset dateTimeOffSetNow, DateTimeOffset latestTimeToReachSoc)
    {
        var activeCharge = car.CarState.PlannedChargingSlots.FirstOrDefault(p => p.IsActive);
        var activeChargeStartedBeforeLatestTimeToReachSoc =
            activeCharge != default && activeCharge.ChargeStart < latestTimeToReachSoc;
        return !((chargeDurationToMinSoc > TimeSpan.Zero)
               && (latestTimeToReachSoc < (dateTimeOffSetNow + chargeDurationToMinSoc))
               && (activeChargeStartedBeforeLatestTimeToReachSoc || dateTimeOffSetNow < latestTimeToReachSoc));
    }

    internal List<DtoChargingSlot> ConcatenateChargeTimes(List<DtoChargingSlot> chargingSlotsBeforeConcatenation)
    {
        _logger.LogTrace("{method}({@chargingSlotsBeforeConcatenation})", nameof(ConcatenateChargeTimes), chargingSlotsBeforeConcatenation);
        if (chargingSlotsBeforeConcatenation.Count < 2)
        {
            return chargingSlotsBeforeConcatenation;
        }

        chargingSlotsBeforeConcatenation = chargingSlotsBeforeConcatenation.OrderBy(s => s.ChargeStart).ToList();
        var chargingSlots = new List<DtoChargingSlot>
        {
            chargingSlotsBeforeConcatenation[0],
        };
        for (var i = 1; i < chargingSlotsBeforeConcatenation.Count; i++)
        {
            var currentChargingSlot = chargingSlotsBeforeConcatenation[i];
            var lastChargingSlot = chargingSlots.Last();
            if (lastChargingSlot.ChargeEnd == currentChargingSlot.ChargeStart)
            {
                lastChargingSlot.ChargeEnd = currentChargingSlot.ChargeEnd;
            }
            else
            {
                chargingSlots.Add(currentChargingSlot);
            }
        }
        return chargingSlots;
    }

    private static DtoChargingSlot GenerateChargingSlotBySpotPrice(SpotPrice cheapestPrice, DateTimeOffset minDate, DateTimeOffset maxDate)
    {
        var startTime = new DateTimeOffset(cheapestPrice.StartDate, TimeSpan.Zero);
        if (minDate > startTime)
        {
            startTime = minDate;
        }
        var endTime = new DateTimeOffset(cheapestPrice.EndDate, TimeSpan.Zero);
        if (maxDate < endTime)
        {
            endTime = maxDate;
        }
        var chargingSlot = new DtoChargingSlot()
        {
            ChargeStart = startTime,
            ChargeEnd = endTime,
        };
        return chargingSlot;
    }

    private async Task<List<SpotPrice>> ChargePricesUntilLatestTimeToReachSocOrderedByPrice(DateTimeOffset dateTimeOffSetNow, DateTimeOffset latestTimeToReachSoc)
    {
        var spotPrices = await _teslaSolarChargerContext.SpotPrices.AsNoTracking()
            .Where(s => s.EndDate > dateTimeOffSetNow.UtcDateTime && s.StartDate < latestTimeToReachSoc.UtcDateTime)
            .ToListAsync().ConfigureAwait(false);
        //SqLite can not order decimal
        var orderedSpotPrices = spotPrices.OrderBy(s => s.Price).ToList();
        return orderedSpotPrices;
    }

    public async Task<bool> IsLatestTimeToReachSocAfterLatestKnownChargePrice(int carId)
    {
        var latestTimeToReachSoC = new DateTimeOffset(_settings.Cars.First(c => c.Id == carId).CarConfiguration.LatestTimeToReachSoC, TimeZoneInfo.Local.BaseUtcOffset);
        return await _spotPriceService.LatestKnownSpotPriceTime().ConfigureAwait(false) < latestTimeToReachSoC;
    }

 
}
