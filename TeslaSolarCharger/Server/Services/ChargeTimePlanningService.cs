using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Car = TeslaSolarCharger.Shared.Dtos.Settings.Car;

namespace TeslaSolarCharger.Server.Services;

public class ChargeTimePlanningService : IChargeTimePlanningService
{
    private readonly ILogger<ChargeTimePlanningService> _logger;
    private readonly ISettings _settings;
    private readonly IChargeTimeUpdateService _chargeTimeUpdateService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISpotPriceService _spotPriceService;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;

    public ChargeTimePlanningService(ILogger<ChargeTimePlanningService> logger, ISettings settings,
        IChargeTimeUpdateService chargeTimeUpdateService, IDateTimeProvider dateTimeProvider,
        ISpotPriceService spotPriceService, ITeslaSolarChargerContext teslaSolarChargerContext)
    {
        _logger = logger;
        _settings = settings;
        _chargeTimeUpdateService = chargeTimeUpdateService;
        _dateTimeProvider = dateTimeProvider;
        _spotPriceService = spotPriceService;
        _teslaSolarChargerContext = teslaSolarChargerContext;
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
        var chargeDurationToMinSoc = _chargeTimeUpdateService.CalculateTimeToReachMinSocAtFullSpeedCharge(car);
        if (chargeDurationToMinSoc == TimeSpan.Zero && car.CarConfiguration.ChargeMode != ChargeMode.MaxPower)
        {
            //No charging is planned
        }
        else
        {
            switch (car.CarConfiguration.ChargeMode)
            {
                case ChargeMode.PvAndMinSoc:
                    var plannedChargeSlot = new DtoChargingSlot()
                    {
                        ChargeStart = dateTimeOffSetNow,
                        ChargeEnd = dateTimeOffSetNow + chargeDurationToMinSoc,
                    };

                    plannedChargingSlots.Add(plannedChargeSlot);
                    break;

                case ChargeMode.PvOnly:
                    if (car.CarConfiguration.LatestTimeToReachSoC > dateTimeOffSetNow)
                    {
                        var plannedChargingSlot = new DtoChargingSlot()
                        {
                            ChargeEnd = car.CarConfiguration.LatestTimeToReachSoC,
                            ChargeStart = car.CarConfiguration.LatestTimeToReachSoC - chargeDurationToMinSoc,
                        };
                        plannedChargingSlots.Add(plannedChargingSlot);
                    }

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
                    plannedChargingSlots.AddRange(await GenerateSpotPriceChargingSlots(car, chargeDurationToMinSoc).ConfigureAwait(false));
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return plannedChargingSlots.OrderBy(s => s.ChargeStart).ToList();
    }

    //ToDo: Add Unit Tests for this
    internal async Task<IEnumerable<DtoChargingSlot>> GenerateSpotPriceChargingSlots(Car car, TimeSpan chargeDurationToMinSoc)
    {
        _logger.LogTrace("{method}({carId}, {chargeDurationToMinSoc}", nameof(GenerateSpotPriceChargingSlots), car.Id, chargeDurationToMinSoc);
        var chargingSlots = new List<DtoChargingSlot>();
        var latestTimeToReachSoc = new DateTimeOffset(car.CarConfiguration.LatestTimeToReachSoC, TimeZoneInfo.Local.BaseUtcOffset);
        var chargePricesUntilLatestTimeToReachSocOrderedByPrice =
            await ChargePricesUntilLatestTimeToReachSocOrderedByPrice(_dateTimeProvider.DateTimeOffSetNow(), latestTimeToReachSoc).ConfigureAwait(false);
        if (await _spotPriceService.LatestKnownSpotPriceTime().ConfigureAwait(false) < latestTimeToReachSoc)
        {
            return chargingSlots;
        }
        
        var hoursNeeded = chargeDurationToMinSoc.TotalHours;
        var fullHoursNeeded = GetFullNeededHours(hoursNeeded);
        var cheapestPrices = chargePricesUntilLatestTimeToReachSocOrderedByPrice.Take(fullHoursNeeded).ToList();
        var chargingSlotsBeforeConcatenation = new List<DtoChargingSlot>();
        foreach (var cheapestPrice in cheapestPrices)
        {
            var chargingSlot = GenerateChargingSlotBySpotPrice(cheapestPrice);
            chargingSlotsBeforeConcatenation.Add(chargingSlot);
        }
        if (chargeDurationToMinSoc.Minutes > 0)
        {
            var lastChargingPriceToAdd = chargePricesUntilLatestTimeToReachSocOrderedByPrice.Skip(fullHoursNeeded).First();
            var chargingSlot = GenerateChargingSlotBySpotPrice(lastChargingPriceToAdd);
            chargingSlot.ChargeStart = chargingSlot.ChargeEnd.AddMinutes(-chargeDurationToMinSoc.Minutes);
            chargingSlotsBeforeConcatenation.Add(chargingSlot);
        }

        //ToDo: Merge chargingSlots if startTime=endTime
        chargingSlots = chargingSlotsBeforeConcatenation;
        return chargingSlots;
    }

    private static DtoChargingSlot GenerateChargingSlotBySpotPrice(SpotPrice cheapestPrice)
    {
        var chargingSlot = new DtoChargingSlot()
        {
            ChargeStart = new DateTimeOffset(cheapestPrice.StartDate, TimeSpan.Zero),
            ChargeEnd = new DateTimeOffset(cheapestPrice.EndDate, TimeSpan.Zero),
        };
        return chargingSlot;
    }

    internal int GetFullNeededHours(double hoursNeeded)
    {
        return (int)hoursNeeded;
    }

    private async Task<List<SpotPrice>> ChargePricesUntilLatestTimeToReachSocOrderedByPrice(DateTimeOffset dateTimeOffSetNow, DateTimeOffset latestTimeToReachSoc)
    {
        var spotPrices = await _teslaSolarChargerContext.SpotPrices
            .Where(s => s.EndDate > dateTimeOffSetNow.UtcDateTime && s.StartDate < latestTimeToReachSoc.UtcDateTime)
            .ToListAsync().ConfigureAwait(false);
        //SqLite can not order decimal
        var orderedSpotPrices = spotPrices.OrderBy(s => s.Price).ToList();
        return orderedSpotPrices;
    }
}
