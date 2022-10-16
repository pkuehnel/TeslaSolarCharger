using System.Runtime.CompilerServices;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources;
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
    private readonly ITeslaMateMqttService _teslaMateMqttService;
    private readonly GlobalConstants _globalConstants;

    public ChargingService(ILogger<ChargingService> logger,
        ISettings settings, IDateTimeProvider dateTimeProvider, ITelegramService telegramService,
        ITeslaService teslaService, IConfigurationWrapper configurationWrapper, IPvValueService pvValueService,
        ITeslaMateMqttService teslaMateMqttService, GlobalConstants globalConstants)
    {
        _logger = logger;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _telegramService = telegramService;
        _teslaService = teslaService;
        _configurationWrapper = configurationWrapper;
        _pvValueService = pvValueService;
        _teslaMateMqttService = teslaMateMqttService;
        _globalConstants = globalConstants;
    }

    public async Task SetNewChargingValues()
    {
        _logger.LogTrace("{method}()", nameof(SetNewChargingValues));

        _logger.LogDebug("Current overage is {overage} Watt.", _settings.Overage);

        var geofence = _configurationWrapper.GeoFence();
        _logger.LogDebug("Relevant Geofence: {geofence}", geofence);

        if (!_teslaMateMqttService.IsMqttClientConnected)
        {
            _logger.LogWarning("TeslaMate Mqtt Client is not connected. Charging Values won't be set.");
        }

        await LogErrorForCarsWithUnknownSocLimit(_settings.Cars).ConfigureAwait(false);

        var relevantCarIds = GetRelevantCarIds();
        _logger.LogDebug("Relevant car ids: {@ids}", relevantCarIds);

        var irrelevantCars = GetIrrelevantCars(relevantCarIds);
        _logger.LogDebug("Irrelevant car ids: {@ids}", irrelevantCars.Select(c => c.Id));

        var relevantCars = _settings.Cars.Where(c => relevantCarIds.Any(r => c.Id == r)).ToList();

        _logger.LogDebug("Relevant cars: {@relevantCars}", relevantCars);
        _logger.LogDebug("Irrelevant cars: {@irrlevantCars}", irrelevantCars);

        if (relevantCarIds.Count < 1)
        {
            _logger.LogDebug("No car was charging this cycle.");
            _settings.ControlledACarAtLastCycle = false;
            return;
        }

        if (_settings.Overage == null)
        {
            _logger.LogWarning("Can not control power as overage is unknown");
            return;
        }

        var powerToControl = CalculatePowerToControl(relevantCars);

        if (!_settings.ControlledACarAtLastCycle)
        {
            //Wait for the car to reach charging Power
            _logger.LogDebug("No car was charging last charge cycle");
            await Task.Delay(TimeSpan.FromSeconds(25)).ConfigureAwait(false);
            powerToControl = CalculatePowerToControl(relevantCars);
            _logger.LogDebug("Power to control: {powerToControl}", powerToControl);
            foreach (var relevantCar in relevantCars)
            {
                _logger.LogDebug("New charging car: {carId}", relevantCar.Id);
                var powerToControlIncludingChargingPower = powerToControl + relevantCar.CarState.ChargingPowerAtHome;
                _logger.LogDebug($"Power to control including charging power: {powerToControl}", powerToControlIncludingChargingPower);
                var minimumChargingPower = relevantCar.CarState.ActualPhases * relevantCar.CarState.ChargerVoltage * relevantCar.CarConfiguration.MinimumAmpere;
                _logger.LogDebug("Minimum charging power {minimumChargingPower}", minimumChargingPower);
                if (powerToControlIncludingChargingPower <
                    minimumChargingPower)
                {
                    _logger.LogDebug("Set Should charge since to early date so car will stop charging.");
                    relevantCar.CarState.ShouldStopChargingSince = new DateTime(2022, 1, 1);
                }
            }
        }

        _logger.LogDebug("At least one car is charging.");
        _settings.ControlledACarAtLastCycle = true;
        
        _logger.LogDebug("Power to control: {power}", powerToControl);

        if (powerToControl < 0)
        {
            _logger.LogTrace("Reversing car order");
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

    private int CalculatePowerToControl(List<Car> relevantCars)
    {
        _logger.LogTrace("{method}({param})", nameof(CalculatePowerToControl), relevantCars);
        var currentControledPower = relevantCars
            .Sum(c => c.CarState.ChargingPower);
        _logger.LogDebug("Current controlled Power: {power}", currentControledPower);

        var buffer = _configurationWrapper.PowerBuffer();
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);

        var averagedOverage = _pvValueService.GetAveragedOverage();
        _logger.LogDebug("Averaged overage {averagedOverage}", averagedOverage);

        var overage = averagedOverage - buffer;
        _logger.LogDebug("Overage after subtracting power buffer ({buffer}): {overage}", buffer, overage);

        var homeBatteryMinSoc = _configurationWrapper.HomeBatteryMinSoc();
        _logger.LogDebug("Home battery min soc: {homeBatteryMinSoc}", homeBatteryMinSoc);
        var homeBatteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
        _logger.LogDebug("Home battery should charging power: {homeBatteryMaxChargingPower}", homeBatteryMaxChargingPower);
        if (homeBatteryMinSoc != null && homeBatteryMaxChargingPower != null)
        {
            var actualHomeBatterySoc = _settings.HomeBatterySoc;
            _logger.LogDebug("Home battery actual soc: {actualHomeBatterySoc}", actualHomeBatterySoc);
            var actualHomeBatteryPower = _settings.HomeBatteryPower;
            _logger.LogDebug("Home battery actual power: {actualHomeBatteryPower}", actualHomeBatteryPower);
            if (actualHomeBatterySoc != null && actualHomeBatteryPower != null)
            {
                if (actualHomeBatterySoc < homeBatteryMinSoc)
                {
                    overage -= (int)homeBatteryMaxChargingPower - (int)actualHomeBatteryPower;

                    _logger.LogDebug(
                        "Overage after subtracting difference between max home battery charging power ({homeBatteryMaxChargingPower}) and actual home battery charging power ({actualHomeBatteryPower}): {overage}",
                        homeBatteryMaxChargingPower, actualHomeBatteryPower, overage);
                }
                else
                {
                    overage += (int)actualHomeBatteryPower;
                    _logger.LogDebug("Overage after adding home battery power ({actualHomeBatteryPower}): {overage}",
                        actualHomeBatteryPower, overage);
                }
            }
        }

        var powerToControl = overage;
        return powerToControl;
    }

    internal List<Car> GetIrrelevantCars(List<int> relevantCarIds)
    {
        return _settings.Cars.Where(car => !relevantCarIds.Any(i => i == car.Id)).ToList();
    }

    private async Task LogErrorForCarsWithUnknownSocLimit(List<Car> cars)
    {
        foreach (var car in cars)
        {
            var unknownSocLimit = IsSocLimitUnknown(car);
            if (unknownSocLimit && 
                (car.CarState.State == null ||
                 car.CarState.State == CarStateEnum.Unknown ||
                 car.CarState.State == CarStateEnum.Asleep ||
                 car.CarState.State == CarStateEnum.Offline))
            {
                _logger.LogWarning("Unknown charge limit of car {carId}.", car.Id);
                await _telegramService.SendMessage($"Unknown charge limit of car {car.Id}.").ConfigureAwait(false);
            }
        }
    }

    private bool IsSocLimitUnknown(Car car)
    {
        return car.CarConfiguration.SocLimit == null || car.CarConfiguration.SocLimit < _globalConstants.MinSocLimit;
    }


    internal List<int> GetRelevantCarIds()
    {
        var relevantIds = _settings.Cars
            .Where(c =>
                c.CarState.IsHomeGeofence == true
                && c.CarConfiguration.ShouldBeManaged == true
                //next line changed from == true to != false due to issue https://github.com/pkuehnel/TeslaSolarCharger/issues/365
                && c.CarState.PluggedIn != false
                && (c.CarState.ClimateOn == true ||
                    c.CarState.ChargerActualCurrent > 0 ||
                    c.CarState.SoC < c.CarConfiguration.SocLimit - 2))
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
        _logger.LogTrace("{method}({param1}, {param2})", nameof(ChangeCarAmp), car.Id, ampToChange);
        //This might happen if only climate is running or car nearly full which means full power is not needed.
        if (ampToChange > 0 && car.CarState.ChargerRequestedCurrent > car.CarState.ChargerActualCurrent && car.CarState.ChargerActualCurrent > 0)
        {
            //ampToChange = 0;
            _logger.LogWarning("Car does not use full request.");
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
        await SendWarningOnChargerPilotReduced(car, maxAmpPerCar).ConfigureAwait(false);

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
                        car.CarConfiguration.SocLimit - 2)
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
                _logger.LogDebug("Can not stop charging: Climate on: {climateState}, earliest Switch Off: {earliestSwitchOff}",
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
        else if (finalAmpsToSet >= minAmpPerCar && (car.CarState.ChargerActualCurrent is 0 or null))
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

        var timespanUntilSwitchOff = _configurationWrapper.TimespanUntilSwitchOff();
        var earliestSwitchOff = car.CarState.ShouldStopChargingSince + timespanUntilSwitchOff;
        _logger.LogDebug("Should start charging since: {shoudStopChargingSince}", car.CarState.ShouldStopChargingSince);
        _logger.LogDebug("Timespan until switch on: {timespanUntilSwitchOff}", timespanUntilSwitchOff);
        _logger.LogDebug("Earliest switch off: {earliestSwitchOn}", earliestSwitchOff);
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

        var timespanUntilSwitchOn = _configurationWrapper.TimespanUntilSwitchOn();
        var earliestSwitchOn = car.CarState.ShouldStartChargingSince + timespanUntilSwitchOn;
        _logger.LogDebug("Should start charging since: {shoudStartChargingSince}", car.CarState.ShouldStartChargingSince);
        _logger.LogDebug("Timespan until switch on: {timespanUntilSwitchOn}", timespanUntilSwitchOn);
        _logger.LogDebug("Earliest switch on: {earliestSwitchOn}", earliestSwitchOn);
        return earliestSwitchOn;
    }
}
