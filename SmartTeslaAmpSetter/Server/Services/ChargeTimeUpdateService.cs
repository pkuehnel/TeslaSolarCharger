using System.Runtime.CompilerServices;
using SmartTeslaAmpSetter.Server.Contracts;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.TimeProviding;

[assembly: InternalsVisibleTo("SmartTeslaAmpSetter.Tests")]
namespace SmartTeslaAmpSetter.Server.Services;

public class ChargeTimeUpdateService : IChargeTimeUpdateService
{
    private readonly ILogger<ChargeTimeUpdateService> _logger;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ChargeTimeUpdateService(ILogger<ChargeTimeUpdateService> logger, ISettings settings, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
    }

    public void UpdateChargeTimes()
    {
        _logger.LogTrace("{method}()", nameof(UpdateChargeTimes));
        foreach (var car in _settings.Cars)
        {
            UpdateChargeTime(car);
        }
    }

    internal void UpdateChargeTime(Car car)
    {
        var socToCharge = (double) car.CarConfiguration.MinimumSoC - (car.CarState.SoC ?? 0);
        if (car.CarState.PluggedIn != true)
        {
            car.CarState.ReachingMinSocAtFullSpeedCharge = null;
            return;
        }

        if (socToCharge < 1)
        {
            car.CarState.ReachingMinSocAtFullSpeedCharge = _dateTimeProvider.Now();
        }

        var energyToCharge = car.CarConfiguration.UsableEnergy * 1000 * (decimal) (socToCharge / 100.0);
        var numberOfPhases = car.CarState.ChargerPhases > 1 ? 3 : 1;
        var maxChargingPower =
            car.CarConfiguration.MaximumAmpere * numberOfPhases
                                               //Use 230 instead of actual voltage because of 0 Volt if charging is stopped
                                               * 230;
        car.CarState.ReachingMinSocAtFullSpeedCharge =
            _dateTimeProvider.Now() + TimeSpan.FromHours((double) (energyToCharge / maxChargingPower));
    }
}