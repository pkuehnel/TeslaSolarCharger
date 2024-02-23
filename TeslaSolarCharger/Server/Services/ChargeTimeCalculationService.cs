using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Contracts;
using Car = TeslaSolarCharger.Shared.Dtos.Settings.Car;

namespace TeslaSolarCharger.Server.Services;

public class ChargeTimeCalculationService(
    ILogger<ChargeTimeCalculationService> logger,
    IDateTimeProvider dateTimeProvider,
    ISettings settings,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ISpotPriceService spotPriceService,
    ITeslaService teslaService,
    IConstants constants)
    : IChargeTimeCalculationService
{
    public TimeSpan CalculateTimeToReachMinSocAtFullSpeedCharge(Car car)
    {
        logger.LogTrace("{method}({carId})", nameof(CalculateTimeToReachMinSocAtFullSpeedCharge), car.Id);
        var socToCharge = (double)car.CarConfiguration.MinimumSoC - (car.CarState.SoC ?? 0);
        const int needsBalancingSocLimit = 100;
        var balancingTime = TimeSpan.FromMinutes(30);
        //This is needed to let the car charge to actually 100% including balancing
        if (socToCharge < 1 && car.CarState.State == CarStateEnum.Charging && car.CarConfiguration.MinimumSoC == needsBalancingSocLimit)
        {
            logger.LogDebug("Continue to charge car as Minimum soc is {balancingSoc}%", needsBalancingSocLimit);
            return balancingTime;
        }
        if (socToCharge < 1 || (socToCharge < constants.MinimumSocDifference && car.CarState.State != CarStateEnum.Charging))
        {
            return TimeSpan.Zero;
        }

        var energyToCharge = car.CarConfiguration.UsableEnergy * 1000 * (decimal)(socToCharge / 100.0);
        var numberOfPhases = car.CarState.ActualPhases;
        var maxChargingPower =
            car.CarConfiguration.MaximumAmpere * numberOfPhases
                                               * (settings.AverageHomeGridVoltage ?? 230);
        var chargeTime = TimeSpan.FromHours((double)(energyToCharge / maxChargingPower));
        if (car.CarConfiguration.MinimumSoC == needsBalancingSocLimit)
        {
            chargeTime += balancingTime;
        }
        return chargeTime;
    }

    public void UpdateChargeTime(Car car)
    {
        car.CarState.ReachingMinSocAtFullSpeedCharge = dateTimeProvider.Now() + CalculateTimeToReachMinSocAtFullSpeedCharge(car);
    }


    public async Task PlanChargeTimesForAllCars()
    {
        logger.LogTrace("{method}()", nameof(PlanChargeTimesForAllCars));
        var carsToPlan = settings.CarsToManage.ToList();
        foreach (var car in carsToPlan)
        {
            await UpdatePlannedChargingSlots(car).ConfigureAwait(false);
            if (car.CarConfiguration.ShouldSetChargeStartTimes != true || car.CarState.IsHomeGeofence != true)
            {
                continue;
            }
#pragma warning disable CS4014
            SetChargeStartIfNeeded(car).ContinueWith(t =>
                logger.LogError(t.Exception, "Could not set planned charge start for car {carId}.", car.Id), TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014
        }
    }

    private async Task SetChargeStartIfNeeded(Car car)
    {
        logger.LogTrace("{method}({carId})", nameof(SetChargeStartIfNeeded), car.Id);
        if (car.CarState.State == CarStateEnum.Charging)
        {
            logger.LogTrace("Do not set charge start in TeslaApp as car is currently charging");
            return;
        }
        try
        {
            var nextPlannedCharge = car.CarState.PlannedChargingSlots.MinBy(c => c.ChargeStart);
            if (nextPlannedCharge == default || nextPlannedCharge.ChargeStart <= dateTimeProvider.DateTimeOffSetNow() || nextPlannedCharge.IsActive)
            {
                await teslaService.SetScheduledCharging(car.Id, null).ConfigureAwait(false);
                return;
            }
            await teslaService.SetScheduledCharging(car.Id, nextPlannedCharge.ChargeStart).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not set planned charge start for car {carId}.", car.Id);
        }
    }

    public async Task UpdatePlannedChargingSlots(Car car)
    {
        logger.LogTrace("{method}({carId}", nameof(UpdatePlannedChargingSlots), car.Id);
        var dateTimeOffSetNow = dateTimeProvider.DateTimeOffSetNow();

        var plannedChargingSlots = await PlanChargingSlots(car, dateTimeOffSetNow).ConfigureAwait(false);
        ReplaceFirstChargingSlotStartTimeIfAlreadyActive(car, plannedChargingSlots, dateTimeOffSetNow);

        //ToDo: if no new planned charging slot and one chargingslot is active, do not remove it if min soc is not reached.
        car.CarState.PlannedChargingSlots = plannedChargingSlots;

    }

    private void ReplaceFirstChargingSlotStartTimeIfAlreadyActive(Car car, List<DtoChargingSlot> plannedChargingSlots,
        DateTimeOffset dateTimeOffSetNow)
    {
        logger.LogTrace("{method}({carId}, {@plannedChargingSlots}, {dateTimeOffSetNow}", nameof(ReplaceFirstChargingSlotStartTimeIfAlreadyActive), car.Id, plannedChargingSlots, dateTimeOffSetNow);
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
        logger.LogTrace("{method}({carId}, {dateTimeOffset}", nameof(PlanChargingSlots), car.Id, dateTimeOffSetNow);
        var plannedChargingSlots = new List<DtoChargingSlot>();
        var chargeDurationToMinSoc = CalculateTimeToReachMinSocAtFullSpeedCharge(car);
        var timeZoneOffset = TimeSpan.Zero;
        if (car.CarConfiguration.LatestTimeToReachSoC.Kind != DateTimeKind.Utc)
        {
            timeZoneOffset = TimeZoneInfo.Local.GetUtcOffset(car.CarConfiguration.LatestTimeToReachSoC);
        }
        var latestTimeToReachSoc =
            new DateTimeOffset(car.CarConfiguration.LatestTimeToReachSoC, timeZoneOffset);
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
                        ChargeEnd = dateTimeOffSetNow.AddDays(1),
                    });
                    break;

                case ChargeMode.SpotPrice:
                    //ToDo: Plan hours that are cheaper than solar price
                    var chargingSlots = await GenerateSpotPriceChargingSlots(car, chargeDurationToMinSoc, dateTimeOffSetNow, latestTimeToReachSoc).ConfigureAwait(false);
                    plannedChargingSlots.AddRange(chargingSlots);
                    break;
                case ChargeMode.DoNothing:
                    plannedChargingSlots = new List<DtoChargingSlot>();
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
        logger.LogTrace("{method}({carId}, {chargeDurationToMinSoc}", nameof(GenerateSpotPriceChargingSlots), car.Id, chargeDurationToMinSoc);
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
        
        chargingSlots = ReduceNumberOfSpotPricedChargingSessions(chargingSlotsBeforeConcatenation);
        return chargingSlots;
    }

    /// <summary>
    /// Reduces the number of charging session by shifting chargingsessions with less than 1 hour duration to the next charging session if needed.
    /// Note: This method should only be used if there charge prices within one hour are constant
    /// </summary>
    internal List<DtoChargingSlot> ReduceNumberOfSpotPricedChargingSessions(List<DtoChargingSlot> chargingSlotsBeforeConcatenation)
    {
        var chargingSlots = ConcatenateChargeTimes(chargingSlotsBeforeConcatenation);
        if (chargingSlots.Count < 2)
        {
            return chargingSlots;
        }
        var reducedChargingSessionChargingSlots = new List<DtoChargingSlot>
        {
            chargingSlots.First(),
        };
        for (var i = 1; i < chargingSlots.Count; i++)
        {
            var lastChargingSlot = chargingSlots[i - 1];
            var currenChargingSlot = chargingSlots[i];
            //Only move not started charging slots shorter than one hour as otherwise chargetime more hours ago could be reduced and shifted to more expensive hours
            if (lastChargingSlot.ChargeStart < dateTimeProvider.DateTimeOffSetNow().AddMilliseconds(1)
                && lastChargingSlot.ChargeDuration < TimeSpan.FromHours(1)
                && (lastChargingSlot.ChargeEnd - currenChargingSlot.ChargeStart).TotalHours < 1)
            {
                lastChargingSlot.ChargeStart = currenChargingSlot.ChargeStart - lastChargingSlot.ChargeDuration;
                lastChargingSlot.ChargeEnd = currenChargingSlot.ChargeEnd;
            }
            else
            {
                reducedChargingSessionChargingSlots.Add(currenChargingSlot);
            }
        }

        return reducedChargingSessionChargingSlots;
    }

    private bool IsAbleToReachSocInTime(Car car, TimeSpan chargeDurationToMinSoc,
        DateTimeOffset dateTimeOffSetNow, DateTimeOffset latestTimeToReachSoc)
    {
        var activeCharge = car.CarState.PlannedChargingSlots.FirstOrDefault(p => p.IsActive);
        var activeChargeStartedBeforeLatestTimeToReachSoc =
            activeCharge != default && activeCharge.ChargeStart < latestTimeToReachSoc;
        return !((chargeDurationToMinSoc > TimeSpan.Zero)
               && (latestTimeToReachSoc < (dateTimeOffSetNow + chargeDurationToMinSoc))
               && (activeChargeStartedBeforeLatestTimeToReachSoc || (dateTimeOffSetNow < latestTimeToReachSoc)));
    }

    internal List<DtoChargingSlot> ConcatenateChargeTimes(List<DtoChargingSlot> chargingSlotsBeforeConcatenation)
    {
        logger.LogTrace("{method}({@chargingSlotsBeforeConcatenation})", nameof(ConcatenateChargeTimes), chargingSlotsBeforeConcatenation);
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
        var spotPrices = await teslaSolarChargerContext.SpotPrices.AsNoTracking()
            .Where(s => s.EndDate > dateTimeOffSetNow.UtcDateTime && s.StartDate < latestTimeToReachSoc.UtcDateTime)
            .ToListAsync().ConfigureAwait(false);
        //SqLite can not order decimal
        var orderedSpotPrices = spotPrices.OrderBy(s => s.Price).ToList();
        return orderedSpotPrices;
    }

    public async Task<bool> IsLatestTimeToReachSocAfterLatestKnownChargePrice(int carId)
    {
        var carConfigurationLatestTimeToReachSoC = settings.Cars.First(c => c.Id == carId).CarConfiguration.LatestTimeToReachSoC;
        var latestTimeToReachSoC = new DateTimeOffset(carConfigurationLatestTimeToReachSoC, TimeZoneInfo.Local.GetUtcOffset(carConfigurationLatestTimeToReachSoC));
        return await spotPriceService.LatestKnownSpotPriceTime().ConfigureAwait(false) < latestTimeToReachSoC;
    }


}
