using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class ChargeTimeCalculationService : IChargeTimeCalculationService
{
    private readonly ILogger<ChargeTimeCalculationService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ChargeTimeCalculationService(ILogger<ChargeTimeCalculationService> logger, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public TimeSpan CalculateTimeToReachMinSocAtFullSpeedCharge(Car car)
    {
        _logger.LogTrace("{method}({carId})", nameof(CalculateTimeToReachMinSocAtFullSpeedCharge), car.Id);
        var socToCharge = (double)car.CarConfiguration.MinimumSoC - (car.CarState.SoC ?? 0);
        if (socToCharge < 1)
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
}
