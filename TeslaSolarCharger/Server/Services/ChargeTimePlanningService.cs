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

    public ChargeTimePlanningService(ILogger<ChargeTimePlanningService> logger, ISettings settings,
        IChargeTimeUpdateService chargeTimeUpdateService, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _settings = settings;
        _chargeTimeUpdateService = chargeTimeUpdateService;
        _dateTimeProvider = dateTimeProvider;
    }


    public void PlanChargeTimesForAllCars()
    {
        _logger.LogTrace("{method}()", nameof(PlanChargeTimesForAllCars));
        var carsToPlan = _settings.Cars.Where(c => c.CarConfiguration.ShouldBeManaged == true).ToList();
        foreach (var car in carsToPlan)
        {
            UpdatePlannedChargingSlots(car);
        }
    }

    internal void UpdatePlannedChargingSlots(Car car)
    {
        _logger.LogTrace("{method}({carId}", nameof(UpdatePlannedChargingSlots), car.Id);
        var dateTimeOffSetNow = _dateTimeProvider.DateTimeOffSetNow();

        var plannedChargingSlots = PlanChargingSlots(car, dateTimeOffSetNow);

        
        ReplaceFirstChargingSlotStartTimeIfAlreadyActive(car, plannedChargingSlots, dateTimeOffSetNow);
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

    internal List<DtoChargingSlot> PlanChargingSlots(Car car, DateTimeOffset dateTimeOffSetNow)
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

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return plannedChargingSlots;
    }
}
