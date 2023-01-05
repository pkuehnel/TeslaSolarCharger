using System.Runtime.CompilerServices;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Server.Services;

public class ChargeTimeUpdateService : IChargeTimeUpdateService
{
    private readonly ILogger<ChargeTimeUpdateService> _logger;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IChargingService _chargingService;
    private readonly IConfigurationWrapper _configurationWrapper;

    public ChargeTimeUpdateService(ILogger<ChargeTimeUpdateService> logger, ISettings settings, IDateTimeProvider dateTimeProvider,
        IChargingService chargingService, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _chargingService = chargingService;
        _configurationWrapper = configurationWrapper;
    }

    public void UpdateChargeTimes()
    {
        _logger.LogTrace("{method}()", nameof(UpdateChargeTimes));
        foreach (var car in _settings.Cars)
        {
            UpdateChargeTime(car);
            UpdateShouldStartStopChargingSince(car);
        }
    }

    private void UpdateShouldStartStopChargingSince(Car car)
    {
        var powerToControl = _chargingService.CalculatePowerToControl();
        var ampToSet = _chargingService.CalculateAmpByPowerAndCar(powerToControl, car);
        if (car.CarState.IsHomeGeofence == true)
        {
            var actualCurrent = car.CarState.ChargerActualCurrent ?? 0;
            //This is needed because sometimes actual current is higher than last set amp, leading to higher calculated amp to set, than actually needed
            if (actualCurrent > car.CarState.LastSetAmp)
            {
                actualCurrent = car.CarState.LastSetAmp;
            }
            ampToSet += actualCurrent;
        }
        //Commented section not needed because should start should also be set if charging
        if (ampToSet >= car.CarConfiguration.MinimumAmpere/* && (car.CarState.ChargerActualCurrent is 0 or null)*/)
        {
            SetEarliestSwitchOnToNowWhenNotAlreadySet(car);
        }
        else
        {
            SetEarliestSwitchOffToNowWhenNotAlreadySet(car);
        }
    }

    internal void SetEarliestSwitchOnToNowWhenNotAlreadySet(Car car)
    {
        _logger.LogTrace("{method}({param1})", nameof(SetEarliestSwitchOnToNowWhenNotAlreadySet), car.Id);
        if (car.CarState.ShouldStartChargingSince == null)
        {
            car.CarState.ShouldStartChargingSince = _dateTimeProvider.Now();
            var timespanUntilSwitchOn = _configurationWrapper.TimespanUntilSwitchOn();
            var earliestSwitchOn = car.CarState.ShouldStartChargingSince + timespanUntilSwitchOn;
            car.CarState.EarliestSwitchOn = earliestSwitchOn;
        }
        car.CarState.EarliestSwitchOff = null;
        car.CarState.ShouldStopChargingSince = null;
        _logger.LogDebug("Should start charging since: {shoudStartChargingSince}", car.CarState.ShouldStartChargingSince);
        _logger.LogDebug("Earliest switch on: {earliestSwitchOn}", car.CarState.EarliestSwitchOn);
    }

    internal void SetEarliestSwitchOffToNowWhenNotAlreadySet(Car car)
    {
        _logger.LogTrace("{method}({param1})", nameof(SetEarliestSwitchOffToNowWhenNotAlreadySet), car.Id);
        if (car.CarState.ShouldStopChargingSince == null)
        {
            car.CarState.ShouldStopChargingSince = _dateTimeProvider.Now();
            var timespanUntilSwitchOff = _configurationWrapper.TimespanUntilSwitchOff();
            var earliestSwitchOff = car.CarState.ShouldStopChargingSince + timespanUntilSwitchOff;
            car.CarState.EarliestSwitchOff = earliestSwitchOff;
        }
        car.CarState.EarliestSwitchOn = null;
        car.CarState.ShouldStartChargingSince = null;
        _logger.LogDebug("Should start charging since: {shoudStartChargingSince}", car.CarState.ShouldStartChargingSince);
        _logger.LogDebug("Earliest switch on: {earliestSwitchOn}", car.CarState.EarliestSwitchOff);
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
