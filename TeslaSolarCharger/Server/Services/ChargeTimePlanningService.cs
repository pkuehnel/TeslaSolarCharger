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
            PlanChargingSlots(car);
        }
    }

    private void PlanChargingSlots(Car car)
    {
        _logger.LogTrace("{method}({carId}", nameof(PlanChargingSlots), car.Id);
        var chargeDurationToMinSoc = _chargeTimeUpdateService.CalculateTimeToReachMinSocAtFullSpeedCharge(car);
        var plannedChargingSlots = new List<DtoChargingSlot>();

        if (chargeDurationToMinSoc == TimeSpan.Zero && car.CarConfiguration.ChargeMode != ChargeMode.MaxPower)
        {
            //No charging is planned
        }
        else switch (car.CarConfiguration.ChargeMode)
        {
            case ChargeMode.PvAndMinSoc:
                var plannedChargeSlot = new DtoChargingSlot()
                {
                    ChargeStart = _dateTimeProvider.DateTimeOffSetNow(),
                    ChargeEnd = _dateTimeProvider.DateTimeOffSetNow() + chargeDurationToMinSoc,
                };

                plannedChargingSlots.Add(plannedChargeSlot);
                break;

            case ChargeMode.PvOnly:
                if (car.CarConfiguration.LatestTimeToReachSoC > _dateTimeProvider.DateTimeOffSetNow())
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
                    ChargeStart = _dateTimeProvider.DateTimeOffSetNow(),
                    ChargeEnd = DateTimeOffset.MaxValue,
                });
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        car.CarState.PlannedChargingSlots = plannedChargingSlots;
    }
}
