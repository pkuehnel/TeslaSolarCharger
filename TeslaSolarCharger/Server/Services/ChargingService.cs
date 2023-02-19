using System.Runtime.CompilerServices;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
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
    private readonly ILatestTimeToReachSocUpdateService _latestTimeToReachSocUpdateService;
    private readonly IChargeTimeCalculationService _chargeTimeCalculationService;

    public ChargingService(ILogger<ChargingService> logger,
        ISettings settings, IDateTimeProvider dateTimeProvider, ITelegramService telegramService,
        ITeslaService teslaService, IConfigurationWrapper configurationWrapper, IPvValueService pvValueService,
        ITeslaMateMqttService teslaMateMqttService, GlobalConstants globalConstants, ILatestTimeToReachSocUpdateService latestTimeToReachSocUpdateService,
        IChargeTimeCalculationService chargeTimeCalculationService)
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
        _latestTimeToReachSocUpdateService = latestTimeToReachSocUpdateService;
        _chargeTimeCalculationService = chargeTimeCalculationService;
    }

    public async Task SetNewChargingValues()
    {
        _logger.LogTrace("{method}()", nameof(SetNewChargingValues));
        await UpdateChargingRelevantValues().ConfigureAwait(false);


        _logger.LogDebug("Current overage is {overage} Watt.", _settings.Overage);
        if (_settings.Overage == null)
        {
            _logger.LogWarning("Can not control power as overage is unknown. Use int minValue");
            _settings.Overage = int.MinValue + 10;
        }
        var geofence = _configurationWrapper.GeoFence();
        _logger.LogDebug("Relevant Geofence: {geofence}", geofence);

        if (!_teslaMateMqttService.IsMqttClientConnected)
        {
            _logger.LogWarning("TeslaMate Mqtt Client is not connected. Charging Values won't be set.");
        }

        LogErrorForCarsWithUnknownSocLimit(_settings.CarsToManage);

        var relevantCarIds = GetRelevantCarIds();
        _logger.LogDebug("Relevant car ids: {@ids}", relevantCarIds);

        var irrelevantCars = GetIrrelevantCars(relevantCarIds);
        _logger.LogDebug("Irrelevant car ids: {@ids}", irrelevantCars.Select(c => c.Id));
        foreach (var irrelevantCar in irrelevantCars)
        {
            SetAllPlannedChargingSlotsToInactive(irrelevantCar);
        }

        var relevantCars = _settings.Cars
            .Where(c => relevantCarIds.Any(r => c.Id == r))
            .OrderBy(c => c.CarConfiguration.ChargingPriority)
            .ThenBy(c => c.Id)
            .ToList();

        _logger.LogDebug("Relevant cars: {@relevantCars}", relevantCars);
        _logger.LogDebug("Irrelevant cars: {@irrlevantCars}", irrelevantCars);

        if (relevantCarIds.Count < 1)
        {
            _logger.LogDebug("No car was charging this cycle.");
            _settings.ControlledACarAtLastCycle = false;
            return;
        }

        var powerToControl = CalculatePowerToControl(_settings.ControlledACarAtLastCycle);

        _logger.LogDebug("At least one car is charging.");
        _settings.ControlledACarAtLastCycle = true;
        
        _logger.LogDebug("Power to control: {power}", powerToControl);

        var maxUsableCurrent = _configurationWrapper.MaxCombinedCurrent();
        var currentlyUsedCurrent = relevantCars.Select(c => c.CarState.ChargerActualCurrent ?? 0).Sum();
        var maxAmpIncrease = new DtoValue<int>(maxUsableCurrent - currentlyUsedCurrent);

        if (powerToControl < 0 || maxAmpIncrease.Value < 0)
        {
            _logger.LogTrace("Reversing car order");
            relevantCars.Reverse();
        }

        

        foreach (var relevantCar in relevantCars)
        {
            var ampToControl = CalculateAmpByPowerAndCar(powerToControl, relevantCar);
            _logger.LogDebug("Amp to control: {amp}", ampToControl);
            _logger.LogDebug("Update Car amp for car {carname}", relevantCar.CarState.Name);
            powerToControl -= await ChangeCarAmp(relevantCar, ampToControl, maxAmpIncrease).ConfigureAwait(false);
        }
    }

    private void SetAllPlannedChargingSlotsToInactive(Car car)
    {
        foreach (var plannedChargingSlot in car.CarState.PlannedChargingSlots)
        {
            plannedChargingSlot.IsActive = false;
        }
    }

    private async Task UpdateChargingRelevantValues()
    {
        UpdateChargeTimes();
        await _chargeTimeCalculationService.PlanChargeTimesForAllCars().ConfigureAwait(false);
        await _latestTimeToReachSocUpdateService.UpdateAllCars().ConfigureAwait(false);
    }

    public int CalculateAmpByPowerAndCar(int powerToControl, Car car)
    {
        //ToDo: replace 230 with actual voltage on location
        return Convert.ToInt32(Math.Floor(powerToControl / ((double)230 * car.CarState.ActualPhases)));
    }

    public int CalculatePowerToControl(bool calculateAverage)
    {
        _logger.LogTrace("{method}({calculateAverage})", nameof(CalculatePowerToControl), calculateAverage);

        var buffer = _configurationWrapper.PowerBuffer();
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);
        var averagedOverage = calculateAverage ? _pvValueService.GetAveragedOverage() : _settings.Overage ?? int.MinValue;
        _logger.LogDebug("Averaged overage {averagedOverage}", averagedOverage);

        var overage = averagedOverage - buffer;
        _logger.LogDebug("Overage after subtracting power buffer ({buffer}): {overage}", buffer, overage);

        overage = AddHomeBatteryStateToPowerCalculation(overage);

        var powerToControl = overage;
        return powerToControl;
    }

    internal int AddHomeBatteryStateToPowerCalculation(int overage)
    {
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
                var batteryMinChargingPower = GetBatteryTargetChargingPower();
                overage -= batteryMinChargingPower - (int)actualHomeBatteryPower;
            }
        }

        return overage;
    }

    public int GetBatteryTargetChargingPower()
    {
        var actualHomeBatterySoc = _settings.HomeBatterySoc;
        var homeBatteryMinSoc = _configurationWrapper.HomeBatteryMinSoc();
        var homeBatteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
        if (actualHomeBatterySoc < homeBatteryMinSoc)
        {
            return homeBatteryMaxChargingPower ?? 0;
        }

        return 0;
    }

    internal List<Car> GetIrrelevantCars(List<int> relevantCarIds)
    {
        return _settings.Cars.Where(car => !relevantCarIds.Any(i => i == car.Id)).ToList();
    }

    private void LogErrorForCarsWithUnknownSocLimit(List<Car> cars)
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
            }
        }
    }

    private bool IsSocLimitUnknown(Car car)
    {
        return car.CarState.SocLimit == null || car.CarState.SocLimit < _globalConstants.MinSocLimit;
    }


    public List<int> GetRelevantCarIds()
    {
        var relevantIds = _settings.Cars
            .Where(c =>
                c.CarState.IsHomeGeofence == true
                && c.CarConfiguration.ShouldBeManaged == true
                //next line changed from == true to != false due to issue https://github.com/pkuehnel/TeslaSolarCharger/issues/365
                && c.CarState.PluggedIn != false
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
    /// <param name="maxAmpIncrease">Max Amp increase (also relevant for full speed charges)</param>
    /// <returns>Power difference</returns>
    private async Task<int> ChangeCarAmp(Car car, int ampToChange, DtoValue<int> maxAmpIncrease)
    {
        _logger.LogTrace("{method}({param1}, {param2}, {param3})", nameof(ChangeCarAmp), car.Id, ampToChange, maxAmpIncrease.Value);
        if (maxAmpIncrease.Value < ampToChange)
        {
            _logger.LogDebug("Reduce current increase from {ampToChange}A to {maxAmpIncrease}A due to limited combined charging current.",
                ampToChange, maxAmpIncrease.Value);
            ampToChange = maxAmpIncrease.Value;
        }
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
        

        EnableFullSpeedChargeIfWithinPlannedChargingSlot(car);
        DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(car);

        //Falls MaxPower als Charge Mode: Leistung auf maximal
        if (car.CarConfiguration.ChargeMode == ChargeMode.MaxPower || car.CarState.AutoFullSpeedCharge)
        {
            _logger.LogDebug("Max Power Charging: ChargeMode: {chargeMode}, AutoFullSpeedCharge: {autofullspeedCharge}",
                car.CarConfiguration.ChargeMode, car.CarState.AutoFullSpeedCharge);
            if (car.CarState.ChargerRequestedCurrent != maxAmpPerCar || car.CarState.State != CarStateEnum.Charging || maxAmpIncrease.Value < 0)
            {
                var ampToSet = (maxAmpPerCar - car.CarState.ChargerRequestedCurrent) > maxAmpIncrease.Value ? ((car.CarState.ChargerActualCurrent ?? 0) + maxAmpIncrease.Value) : maxAmpPerCar;
                _logger.LogDebug("Set current to {ampToSet} after considering max car Current {maxAmpPerCar} and maxAmpIncrease {maxAmpIncrease}", ampToSet, maxAmpPerCar, maxAmpIncrease.Value);
                if (car.CarState.ChargerActualCurrent < 1)
                {
                    //Do not start charging when battery level near charge limit
                    if (car.CarState.SoC >=
                        car.CarState.SocLimit - 2)
                    {
                        _logger.LogDebug("Do not start charging for car {carId} as set SoC Limit in your Tesla app needs to be 3% higher than actual SoC", car.Id);
                        return 0;
                    }
                    _logger.LogDebug("Charging schould start.");
                    await _teslaService.StartCharging(car.Id, ampToSet, car.CarState.State).ConfigureAwait(false);
                    ampChange += ampToSet - (car.CarState.ChargerActualCurrent ?? 0);
                }
                else
                {
                    await _teslaService.SetAmp(car.Id, ampToSet).ConfigureAwait(false);
                    ampChange += ampToSet - (car.CarState.ChargerActualCurrent ?? 0);
                }

            }

        }
        //Falls Laden beendet werden soll, aber noch ladend
        else if (finalAmpsToSet < minAmpPerCar && car.CarState.ChargerActualCurrent > 0)
        {
            _logger.LogDebug("Charging should stop");
            //Falls Klima an (Laden nicht deaktivierbar), oder Ausschaltbefehl erst seit Kurzem
            if (car.CarState.ClimateOn == true || car.CarState.EarliestSwitchOff > _dateTimeProvider.Now())
            {
                _logger.LogDebug("Can not stop charging: Climate on: {climateState}, earliest Switch Off: {earliestSwitchOff}",
                    car.CarState.ClimateOn, car.CarState.EarliestSwitchOff);
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
            }
        }
        //Falls Laden beendet ist und beendet bleiben soll
        else if (finalAmpsToSet < minAmpPerCar)
        {
            _logger.LogDebug("Charging should stay stopped");
        }
        //Falls nicht ladend, aber laden soll beginnen
        else if (finalAmpsToSet >= minAmpPerCar && (car.CarState.ChargerActualCurrent is 0 or null))
        {
            _logger.LogDebug("Charging should start");

            if (car.CarState.EarliestSwitchOn <= _dateTimeProvider.Now())
            {
                _logger.LogDebug("Charging is starting");
                var startAmp = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
                await _teslaService.StartCharging(car.Id, startAmp, car.CarState.State).ConfigureAwait(false);
                ampChange += startAmp;
            }
        }
        //Normal Ampere setzen
        else
        {
            _logger.LogDebug("Normal amp set");
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

        maxAmpIncrease.Value -= ampChange;
        return ampChange * (car.CarState.ChargerVoltage ?? 230) * car.CarState.ActualPhases;
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

    internal void DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(Car car)
    {
        var currentDate = _dateTimeProvider.DateTimeOffSetNow();
        var plannedChargeSlotInCurrentTime = car.CarState.PlannedChargingSlots
            .FirstOrDefault(c => c.ChargeStart <= currentDate && c.ChargeEnd > currentDate);
        if (plannedChargeSlotInCurrentTime == default)
        {
            car.CarState.AutoFullSpeedCharge = false;
            foreach (var plannedChargeSlot in car.CarState.PlannedChargingSlots)
            {
                plannedChargeSlot.IsActive = false;
            }
        }
    }

    internal void EnableFullSpeedChargeIfWithinPlannedChargingSlot(Car car)
    {
        var currentDate = _dateTimeProvider.DateTimeOffSetNow();
        var plannedChargeSlotInCurrentTime = car.CarState.PlannedChargingSlots
            .FirstOrDefault(c => c.ChargeStart <= currentDate && c.ChargeEnd > currentDate);
        if (plannedChargeSlotInCurrentTime != default)
        {
            car.CarState.AutoFullSpeedCharge = true;
            plannedChargeSlotInCurrentTime.IsActive = true;
        }
    }

    private void UpdateChargeTimes()
    {
        _logger.LogTrace("{method}()", nameof(UpdateChargeTimes));
        foreach (var car in _settings.CarsToManage)
        {
            _chargeTimeCalculationService.UpdateChargeTime(car);
            UpdateShouldStartStopChargingSince(car);
        }
    }

    private void UpdateShouldStartStopChargingSince(Car car)
    {
        _logger.LogTrace("{method}({carId})", nameof(UpdateShouldStartStopChargingSince), car.Id);
        var powerToControl = CalculatePowerToControl(false);
        var ampToSet = CalculateAmpByPowerAndCar(powerToControl, car);
        _logger.LogTrace("Amp to set: {ampToSet}", ampToSet);
        if (car.CarState.IsHomeGeofence == true)
        {
            var actualCurrent = car.CarState.ChargerActualCurrent ?? 0;
            _logger.LogTrace("Actual current: {actualCurrent}", actualCurrent);
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

    

}
