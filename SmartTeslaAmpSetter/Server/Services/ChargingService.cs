using System.Runtime.CompilerServices;
using SmartTeslaAmpSetter.Server.Contracts;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.Enums;
using SmartTeslaAmpSetter.Shared.TimeProviding;
using Car = SmartTeslaAmpSetter.Shared.Dtos.Settings.Car;

[assembly: InternalsVisibleTo("SmartTeslaAmpSetter.Tests")]
namespace SmartTeslaAmpSetter.Server.Services;

public class ChargingService : IChargingService
{
    private readonly ILogger<ChargingService> _logger;
    private readonly IGridService _gridService;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITelegramService _telegramService;
    private readonly ITeslaService _teslaService;
    private readonly IConfigurationWrapper _configurationWrapper;

    public ChargingService(ILogger<ChargingService> logger, IGridService gridService,
        ISettings settings, IDateTimeProvider dateTimeProvider, ITelegramService telegramService,
        ITeslaService teslaService, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _gridService = gridService;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _telegramService = telegramService;
        _teslaService = teslaService;
        _configurationWrapper = configurationWrapper;
    }

    public async Task SetNewChargingValues(bool onlyUpdateValues = false)
    {
        _logger.LogTrace("{method}({param})", nameof(SetNewChargingValues), onlyUpdateValues);

        var overage = await _gridService.GetCurrentOverage().ConfigureAwait(false);

        _settings.Overage = overage;

        _logger.LogDebug($"Current overage is {overage} Watt.");

        var inverterPower = await _gridService.GetCurrentInverterPower().ConfigureAwait(false);

        _settings.InverterPower = inverterPower;

        _logger.LogDebug($"Current overage is {overage} Watt.");

        var buffer = _configurationWrapper.PowerBuffer();
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);

        overage -= buffer;

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

        if (onlyUpdateValues)
        {
            return;
        }

        if (relevantCarIds.Count < 1)
        {
            return;
        }

        var currentRegulatedPower = relevantCars
            .Sum(c => c.CarState.ChargingPower);
        _logger.LogDebug("Current regulated Power: {power}", currentRegulatedPower);

        var powerToRegulate = overage;
        _logger.LogDebug("Power to regulate: {power}", powerToRegulate);

        var ampToRegulate = Convert.ToInt32(Math.Floor(powerToRegulate / ((double)230 * 3)));
        _logger.LogDebug("Amp to regulate: {amp}", ampToRegulate);
        
        if (ampToRegulate < 0)
        {
            _logger.LogDebug("Reversing car order");
            relevantCars.Reverse();
        }

        foreach (var relevantCar in relevantCars)
        {
            _logger.LogDebug("Update Car amp for car {carname}", relevantCar.CarState.Name);
            ampToRegulate -= await ChangeCarAmp(relevantCar, ampToRegulate).ConfigureAwait(false);
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
    
    private async Task<int> ChangeCarAmp(Car relevantCar, int ampToRegulate)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(ChangeCarAmp), relevantCar.CarState.Name, ampToRegulate);
        var finalAmpsToSet = (relevantCar.CarState.ChargerActualCurrent?? 0) + ampToRegulate;
        _logger.LogDebug("Amps to set: {amps}", finalAmpsToSet);
        var ampChange = 0;
        var minAmpPerCar = relevantCar.CarConfiguration.MinimumAmpere;
        var maxAmpPerCar = relevantCar.CarConfiguration.MaximumAmpere;
        _logger.LogDebug("Min amp for car: {amp}", minAmpPerCar);
        _logger.LogDebug("Max amp for car: {amp}", maxAmpPerCar);
        
        EnableFullSpeedChargeIfMinimumSocNotReachable(relevantCar);
        DisableFullSpeedChargeIfMinimumSocReachedOrMinimumSocReachable(relevantCar);

        //Falls MaxPower als Charge Mode: Leistung auf maximal
        if (relevantCar.CarConfiguration.ChargeMode == ChargeMode.MaxPower || relevantCar.CarState.AutoFullSpeedCharge)
        {
            _logger.LogDebug("Max Power Charging: ChargeMode: {chargeMode}, AutoFullSpeedCharge: {autofullspeedCharge}", 
                relevantCar.CarConfiguration.ChargeMode, relevantCar.CarState.AutoFullSpeedCharge);
            if (relevantCar.CarState.ChargerActualCurrent < maxAmpPerCar)
            {
                var ampToSet = maxAmpPerCar;

                if (relevantCar.CarState.ChargerActualCurrent < 1)
                {
                    //Do not start charging when battery level near charge limit
                    if (relevantCar.CarState.SoC >=
                        relevantCar.CarState.SocLimit - 2)
                    {
                        return ampChange;
                    }
                    await _teslaService.StartCharging(relevantCar.Id, ampToSet, relevantCar.CarState.State).ConfigureAwait(false);
                    ampChange += ampToSet - (relevantCar.CarState.ChargerActualCurrent?? 0);
                    UpdateEarliestTimesAfterSwitch(relevantCar.Id);
                }
                else
                {
                    await _teslaService.SetAmp(relevantCar.Id, ampToSet).ConfigureAwait(false);
                    ampChange += ampToSet - (relevantCar.CarState.ChargerActualCurrent?? 0);
                    UpdateEarliestTimesAfterSwitch(relevantCar.Id);
                }

            }

        }
        //Falls Laden beendet werden soll, aber noch ladend
        else if (finalAmpsToSet < minAmpPerCar && relevantCar.CarState.ChargerActualCurrent > 0)
        {
            _logger.LogDebug("Charging should stop");
            var earliestSwitchOff = EarliestSwitchOff(relevantCar.Id);
            //Falls Klima an (Laden nicht deaktivierbar), oder Ausschaltbefehl erst seit Kurzem
            if (relevantCar.CarState.ClimateOn == true || earliestSwitchOff > DateTime.Now)
            {
                _logger.LogDebug("Can not stop charing: Climate on: {climateState}, earliest Switch Off: {earliestSwitchOff}",
                    relevantCar.CarState.ClimateOn,
                    earliestSwitchOff);
                if (relevantCar.CarState.ChargerActualCurrent != minAmpPerCar)
                {
                    await _teslaService.SetAmp(relevantCar.Id, minAmpPerCar).ConfigureAwait(false);
                }
                ampChange += minAmpPerCar - (relevantCar.CarState.ChargerActualCurrent?? 0);
            }
            //Laden Stoppen
            else
            {
                _logger.LogDebug("Stop Charging");
                await _teslaService.StopCharging(relevantCar.Id).ConfigureAwait(false);
                ampChange -= relevantCar.CarState.ChargerActualCurrent ?? 0;
                UpdateEarliestTimesAfterSwitch(relevantCar.Id);
            }
        }
        //Falls Laden beendet ist und beendet bleiben soll
        else if (finalAmpsToSet < minAmpPerCar)
        {
            _logger.LogDebug("Charging should stay stopped");
            UpdateEarliestTimesAfterSwitch(relevantCar.Id);
        }
        //Falls nicht ladend, aber laden soll beginnen
        else if (finalAmpsToSet >= minAmpPerCar && relevantCar.CarState.ChargerActualCurrent == 0)
        {
            _logger.LogDebug("Charging should start");
            var earliestSwitchOn = EarliestSwitchOn(relevantCar.Id);

            if (earliestSwitchOn <= DateTime.Now)
            {
                _logger.LogDebug("Charging should start");
                var startAmp = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
                await _teslaService.StartCharging(relevantCar.Id, startAmp, relevantCar.CarState.State).ConfigureAwait(false);
                ampChange += startAmp;
                UpdateEarliestTimesAfterSwitch(relevantCar.Id);
            }
        }
        //Normal Ampere setzen
        else
        {
            _logger.LogDebug("Normal amp set");
            UpdateEarliestTimesAfterSwitch(relevantCar.Id);
            var ampToSet = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
            if (ampToSet != relevantCar.CarState.ChargerActualCurrent)
            {
                await _teslaService.SetAmp(relevantCar.Id, ampToSet).ConfigureAwait(false);
                ampChange += ampToSet - (relevantCar.CarState.ChargerActualCurrent ?? 0);
            }
            else
            {
                _logger.LogDebug("Current actual amp: {currentActualAmp} same as amp to set: {ampToSet} Do not change anything",
                    relevantCar.CarState.ChargerActualCurrent, ampToSet);
            }
        }

        return ampChange;
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
        car.CarState.ShouldStopChargingSince = DateTime.MaxValue;
        car.CarState.ShouldStartChargingSince = DateTime.MaxValue;
    }

    private DateTime EarliestSwitchOff(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(EarliestSwitchOff), carId);
        var timeSpanUntilSwitchOff = _configurationWrapper.TimespanUntilSwitchOff();
        var car = _settings.Cars.First(c => c.Id == carId);
        if (car.CarState.ShouldStopChargingSince == DateTime.MaxValue)
        {
            car.CarState.ShouldStopChargingSince = DateTime.Now + timeSpanUntilSwitchOff;
        }

        var earliestSwitchOff = car.CarState.ShouldStopChargingSince;
        return earliestSwitchOff;
    }

    private DateTime EarliestSwitchOn(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(EarliestSwitchOn), carId);
        var timeSpanUntilSwitchOn = _configurationWrapper.TimeUntilSwitchOn();
        var car = _settings.Cars.First(c => c.Id == carId);
        if (car.CarState.ShouldStartChargingSince == DateTime.MaxValue)
        {
            car.CarState.ShouldStartChargingSince = DateTime.Now + timeSpanUntilSwitchOn;
        }

        var earliestSwitchOn = car.CarState.ShouldStartChargingSince;
        return earliestSwitchOn;
    }
}