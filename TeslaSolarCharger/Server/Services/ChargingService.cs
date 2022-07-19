using System.Runtime.CompilerServices;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Car = TeslaSolarCharger.Shared.Dtos.Settings.Car;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Server.Services;

public class ChargingService : IChargingService
{
    private readonly ILogger<ChargingService> _logger;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITelegramService _telegramService;
    private readonly ITeslaService _teslaService;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IPvValueService _pvValueService;

    public ChargingService(ILogger<ChargingService> logger,
        ISettings settings, IDateTimeProvider dateTimeProvider, ITelegramService telegramService,
        ITeslaService teslaService, IConfigurationWrapper configurationWrapper, IPvValueService pvValueService)
    {
        _logger = logger;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _telegramService = telegramService;
        _teslaService = teslaService;
        _configurationWrapper = configurationWrapper;
        _pvValueService = pvValueService;
    }

    public async Task SetNewChargingValues()
    {
        _logger.LogTrace("{method}()", nameof(SetNewChargingValues));

        _logger.LogDebug("Current overage is {overage} Watt.", _settings.Overage);

        var geofence = _configurationWrapper.GeoFence();
        _logger.LogDebug("Relevant Geofence: {geofence}", geofence);

        await WakeupCarsWithUnknownSocLimit(_settings.Cars);

        var relevantCarIds = GetRelevantCarIds(geofence);
        _logger.LogDebug("Relevant car ids: {@ids}", relevantCarIds);

        var irrelevantCars = GetIrrelevantCars(relevantCarIds);
        _logger.LogDebug("Irrelevant car ids: {@ids}", irrelevantCars.Select(c => c.Id));

        var relevantCars = _settings.Cars.Where(c => relevantCarIds.Any(r => c.Id == r)).ToList();

        _logger.LogTrace("Relevant cars: {@relevantCars}", relevantCars);
        _logger.LogTrace("Irrelevant cars: {@irrlevantCars}", irrelevantCars);

        UpdateChargingPowerAtHome(geofence);

        if (relevantCarIds.Count < 1)
        {
            return;
        }

        var currentControledPower = relevantCars
            .Sum(c => c.CarState.ChargingPower);
        _logger.LogDebug("Current control Power: {power}", currentControledPower);

        var buffer = _configurationWrapper.PowerBuffer();
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);

        var averagedOverage = _pvValueService.GetAveragedOverage();
        _logger.LogDebug("Averaged overage {averagedOverage}", averagedOverage);

        if (_settings.Overage == null)
        {
            _logger.LogWarning("Can not control power as overage is unknown");
            return;
        }

        var overage = averagedOverage - buffer;
        
        var powerToControl = overage;
        
        _logger.LogDebug("Power to control: {power}", powerToControl);

        if (powerToControl < 0)
        {
            _logger.LogDebug("Reversing car order");
            relevantCars.Reverse();
        }

        foreach (var relevantCar in relevantCars)
        {
            var ampToControl = Convert.ToInt32(Math.Floor(powerToControl / ((double)230 * (relevantCar.CarState.ActualPhases ?? 3))));
            _logger.LogDebug("Amp to control: {amp}", ampToControl);
            _logger.LogDebug("Update Car amp for car {carname}", relevantCar.CarState.Name);
            powerToControl -= await ChangeCarAmp(relevantCar, ampToControl).ConfigureAwait(false);
        }
    }

    private void UpdateChargingPowerAtHome(string geofence)
    {
        var carsAtHome = _settings.Cars.Where(c => c.CarState.Geofence == geofence).ToList();
        foreach (var car in carsAtHome)
        {
            car.CarState.ChargingPowerAtHome = car.CarState.ChargingPower;
        }
        var carsNotAtHome = _settings.Cars.Where(car => !carsAtHome.Select(c => c.Id).Any(i => i == car.Id)).ToList();

        foreach (var car in carsNotAtHome)
        {
            car.CarState.ChargingPowerAtHome = 0;
        }

        //Do not combine with irrelevant cars because then charging would never start
        foreach (var pluggedOutCar in _settings.Cars
                     .Where(c => c.CarState.PluggedIn != true).ToList())
        {
            _logger.LogDebug("Resetting ChargeStart and ChargeStop for car {carId}", pluggedOutCar.Id);
            UpdateEarliestTimesAfterSwitch(pluggedOutCar.Id);
            pluggedOutCar.CarState.ChargingPowerAtHome = 0;
        }
    }

    internal List<Car> GetIrrelevantCars(List<int> relevantCarIds)
    {
        return _settings.Cars.Where(car => !relevantCarIds.Any(i => i == car.Id)).ToList();
    }

    private async Task WakeupCarsWithUnknownSocLimit(List<Car> cars)
    {
        foreach (var car in cars)
        {
            var unknownSocLimit = IsSocLimitUnknown(car);
            if (unknownSocLimit)
            {
                _logger.LogWarning("Unknown charge limit of car {carId}. Waking up car.", car.Id);
                await _telegramService.SendMessage($"Unknown charge limit of car {car.Id}. Waking up car.");
                await _teslaService.WakeUpCar(car.Id).ConfigureAwait(false);
            }
        }
    }

    private bool IsSocLimitUnknown(Car car)
    {
        return car.CarState.SocLimit == null || car.CarState.SocLimit < 50;
    }


    internal List<int> GetRelevantCarIds(string geofence)
    {
        var relevantIds = _settings.Cars
            .Where(c =>
                c.CarState.Geofence == geofence
                && c.CarConfiguration.ShouldBeManaged == true
                && c.CarState.PluggedIn == true
                && (c.CarState.ClimateOn == true ||
                    c.CarState.ChargerActualCurrent > 0 ||
                    c.CarState.SoC < c.CarState.SocLimit - 2))
            .Select(c => c.Id)
            .ToList();

        return relevantIds;
    }

    /// <summary>
    /// Changes ampere of car
    /// </summary>
    /// <param name="car">car whose Ampere should be changed</param>
    /// <param name="ampToChange">Needed amp difference</param>
    /// <returns>Power difference</returns>
    private async Task<int> ChangeCarAmp(Car car, int ampToChange)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(ChangeCarAmp), car.CarState.Name, ampToChange);
        //This might happen if only climate is running or car nearly full which means full power is not needed.
        if (ampToChange > 0 && car.CarState.ChargerRequestedCurrent > car.CarState.ChargerActualCurrent && car.CarState.ChargerActualCurrent > 0)
        {
            ampToChange = 0;
            _logger.LogDebug("Set amp to change to {ampToChange} as car does not use full request.", ampToChange);
        }
        var finalAmpsToSet = (car.CarState.ChargerRequestedCurrent ?? 0) + ampToChange;

        if (car.CarState.ChargerActualCurrent == 0)
        {
            finalAmpsToSet = (int)(car.CarState.ChargerActualCurrent + ampToChange);
        }

        _logger.LogDebug("Amps to set: {amps}", finalAmpsToSet);
        var ampChange = 0;
        var minAmpPerCar = car.CarConfiguration.MinimumAmpere;
        var maxAmpPerCar = car.CarConfiguration.MaximumAmpere;
        _logger.LogDebug("Min amp for car: {amp}", minAmpPerCar);
        _logger.LogDebug("Max amp for car: {amp}", maxAmpPerCar);
        await SendWarningOnChargerPilotReduced(car, maxAmpPerCar);

        if (car.CarState.ChargerPilotCurrent != null)
        {
            if (minAmpPerCar > car.CarState.ChargerPilotCurrent)
            {
                minAmpPerCar = (int)car.CarState.ChargerPilotCurrent;
            }
            if (maxAmpPerCar > car.CarState.ChargerPilotCurrent)
            {
                maxAmpPerCar = (int)car.CarState.ChargerPilotCurrent;
            }
        }
        

        EnableFullSpeedChargeIfMinimumSocNotReachable(car);
        DisableFullSpeedChargeIfMinimumSocReachedOrMinimumSocReachable(car);

        //Falls MaxPower als Charge Mode: Leistung auf maximal
        if (car.CarConfiguration.ChargeMode == ChargeMode.MaxPower || car.CarState.AutoFullSpeedCharge)
        {
            _logger.LogDebug("Max Power Charging: ChargeMode: {chargeMode}, AutoFullSpeedCharge: {autofullspeedCharge}",
                car.CarConfiguration.ChargeMode, car.CarState.AutoFullSpeedCharge);
            if (car.CarState.ChargerRequestedCurrent < maxAmpPerCar)
            {
                var ampToSet = maxAmpPerCar;

                if (car.CarState.ChargerActualCurrent < 1)
                {
                    //Do not start charging when battery level near charge limit
                    if (car.CarState.SoC >=
                        car.CarState.SocLimit - 2)
                    {
                        return 0;
                    }
                    await _teslaService.StartCharging(car.Id, ampToSet, car.CarState.State).ConfigureAwait(false);
                    ampChange += ampToSet - (car.CarState.ChargerActualCurrent ?? 0);
                    UpdateEarliestTimesAfterSwitch(car.Id);
                }
                else
                {
                    await _teslaService.SetAmp(car.Id, ampToSet).ConfigureAwait(false);
                    ampChange += ampToSet - (car.CarState.ChargerActualCurrent ?? 0);
                    UpdateEarliestTimesAfterSwitch(car.Id);
                }

            }

        }
        //Falls Laden beendet werden soll, aber noch ladend
        else if (finalAmpsToSet < minAmpPerCar && car.CarState.ChargerActualCurrent > 0)
        {
            _logger.LogDebug("Charging should stop");
            var earliestSwitchOff = EarliestSwitchOff(car.Id);
            //Falls Klima an (Laden nicht deaktivierbar), oder Ausschaltbefehl erst seit Kurzem
            if (car.CarState.ClimateOn == true || earliestSwitchOff > DateTime.Now)
            {
                _logger.LogDebug("Can not stop charing: Climate on: {climateState}, earliest Switch Off: {earliestSwitchOff}",
                    car.CarState.ClimateOn,
                    earliestSwitchOff);
                if (car.CarState.ChargerActualCurrent != minAmpPerCar)
                {
                    await _teslaService.SetAmp(car.Id, minAmpPerCar).ConfigureAwait(false);
                }
                ampChange += minAmpPerCar - (car.CarState.ChargerActualCurrent ?? 0);
            }
            //Laden Stoppen
            else
            {
                _logger.LogDebug("Stop Charging");
                await _teslaService.StopCharging(car.Id).ConfigureAwait(false);
                ampChange -= car.CarState.ChargerActualCurrent ?? 0;
                UpdateEarliestTimesAfterSwitch(car.Id);
            }
        }
        //Falls Laden beendet ist und beendet bleiben soll
        else if (finalAmpsToSet < minAmpPerCar)
        {
            _logger.LogDebug("Charging should stay stopped");
            UpdateEarliestTimesAfterSwitch(car.Id);
        }
        //Falls nicht ladend, aber laden soll beginnen
        else if (finalAmpsToSet >= minAmpPerCar && car.CarState.ChargerActualCurrent == 0)
        {
            _logger.LogDebug("Charging should start");
            var earliestSwitchOn = EarliestSwitchOn(car.Id);

            if (earliestSwitchOn <= DateTime.Now)
            {
                _logger.LogDebug("Charging is starting");
                var startAmp = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
                await _teslaService.StartCharging(car.Id, startAmp, car.CarState.State).ConfigureAwait(false);
                ampChange += startAmp;
                UpdateEarliestTimesAfterSwitch(car.Id);
            }
        }
        //Normal Ampere setzen
        else
        {
            _logger.LogDebug("Normal amp set");
            UpdateEarliestTimesAfterSwitch(car.Id);
            var ampToSet = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
            if (ampToSet != car.CarState.ChargerRequestedCurrent)
            {
                await _teslaService.SetAmp(car.Id, ampToSet).ConfigureAwait(false);
                ampChange += ampToSet - (car.CarState.ChargerActualCurrent ?? 0);
            }
            else
            {
                _logger.LogDebug("Current requested amp: {currentRequestedAmp} same as amp to set: {ampToSet} Do not change anything",
                    car.CarState.ChargerRequestedCurrent, ampToSet);
            }
        }

        return ampChange * (car.CarState.ChargerVoltage ?? 230) * (car.CarState.ActualPhases ?? 3);
    }

    private async Task SendWarningOnChargerPilotReduced(Car car, int maxAmpPerCar)
    {
        if (car.CarState.ChargerPilotCurrent != null && maxAmpPerCar > car.CarState.ChargerPilotCurrent)
        {
            _logger.LogWarning("Charging speed of {carID} id reduced to {amp}", car.Id, car.CarState.ChargerPilotCurrent);
            if (!car.CarState.ReducedChargeSpeedWarning)
            {
                car.CarState.ReducedChargeSpeedWarning = true;
                await _telegramService
                    .SendMessage(
                        $"Charging of {car.CarState.Name} is reduced to {car.CarState.ChargerPilotCurrent} due to chargelimit of wallbox.")
                    .ConfigureAwait(false);
            }
        }
        else if (car.CarState.ReducedChargeSpeedWarning)
        {
            car.CarState.ReducedChargeSpeedWarning = false;
            await _telegramService.SendMessage($"Charging speed of {car.CarState.Name} is regained.").ConfigureAwait(false);
        }
    }

    internal void DisableFullSpeedChargeIfMinimumSocReachedOrMinimumSocReachable(Car car)
    {
        if (car.CarState.ReachingMinSocAtFullSpeedCharge == null
            || car.CarState.SoC >= car.CarConfiguration.MinimumSoC
            || car.CarState.ReachingMinSocAtFullSpeedCharge < car.CarConfiguration.LatestTimeToReachSoC.AddMinutes(-30)
            && car.CarConfiguration.ChargeMode != ChargeMode.PvAndMinSoc)
        {
            car.CarState.AutoFullSpeedCharge = false;
        }
    }

    internal void EnableFullSpeedChargeIfMinimumSocNotReachable(Car car)
    {
        if (car.CarState.ReachingMinSocAtFullSpeedCharge > car.CarConfiguration.LatestTimeToReachSoC
            && car.CarConfiguration.LatestTimeToReachSoC > _dateTimeProvider.Now()
            || car.CarState.SoC < car.CarConfiguration.MinimumSoC
            && car.CarConfiguration.ChargeMode == ChargeMode.PvAndMinSoc)
        {
            car.CarState.AutoFullSpeedCharge = true;
        }
    }

    private void UpdateEarliestTimesAfterSwitch(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(UpdateEarliestTimesAfterSwitch), carId);
        var car = _settings.Cars.First(c => c.Id == carId);
        car.CarState.ShouldStopChargingSince = null;
        car.CarState.ShouldStartChargingSince = null;
    }

    private DateTime? EarliestSwitchOff(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(EarliestSwitchOff), carId);
        var car = _settings.Cars.First(c => c.Id == carId);
        if (car.CarState.ShouldStopChargingSince == null)
        {
            car.CarState.ShouldStopChargingSince = DateTime.Now;
        }

        var earliestSwitchOff = car.CarState.ShouldStopChargingSince + _configurationWrapper.TimespanUntilSwitchOff();
        return earliestSwitchOff;
    }

    private DateTime? EarliestSwitchOn(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(EarliestSwitchOn), carId);
        var car = _settings.Cars.First(c => c.Id == carId);
        if (car.CarState.ShouldStartChargingSince == null)
        {
            car.CarState.ShouldStartChargingSince = DateTime.Now;
        }

        var earliestSwitchOn = car.CarState.ShouldStartChargingSince + _configurationWrapper.TimespanUntilSwitchOn();
        return earliestSwitchOn;
    }
}