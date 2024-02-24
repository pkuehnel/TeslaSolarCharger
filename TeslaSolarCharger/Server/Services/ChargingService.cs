using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;

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
    private readonly ILatestTimeToReachSocUpdateService _latestTimeToReachSocUpdateService;
    private readonly IChargeTimeCalculationService _chargeTimeCalculationService;
    private readonly IConstants _constants;
    private readonly ITeslamateContext _teslamateContext;

    public ChargingService(ILogger<ChargingService> logger,
        ISettings settings, IDateTimeProvider dateTimeProvider, ITelegramService telegramService,
        ITeslaService teslaService, IConfigurationWrapper configurationWrapper, IPvValueService pvValueService,
        ITeslaMateMqttService teslaMateMqttService, ILatestTimeToReachSocUpdateService latestTimeToReachSocUpdateService,
        IChargeTimeCalculationService chargeTimeCalculationService, IConstants constants,
        ITeslamateContext teslamateContext)
    {
        _logger = logger;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _telegramService = telegramService;
        _teslaService = teslaService;
        _configurationWrapper = configurationWrapper;
        _pvValueService = pvValueService;
        _teslaMateMqttService = teslaMateMqttService;
        _latestTimeToReachSocUpdateService = latestTimeToReachSocUpdateService;
        _chargeTimeCalculationService = chargeTimeCalculationService;
        _constants = constants;
        _teslamateContext = teslamateContext;
    }

    public async Task SetNewChargingValues()
    {
        _logger.LogTrace("{method}()", nameof(SetNewChargingValues));
        await UpdateChargingRelevantValues().ConfigureAwait(false);


        _logger.LogDebug("Current overage is {overage} Watt.", _settings.Overage);
        if (_settings.Overage == null && _settings.InverterPower == null)
        {
            _logger.LogWarning("Can not control power as overage is unknown. Use int minValue");
        }
        var geofence = _configurationWrapper.GeoFence();
        _logger.LogDebug("Relevant Geofence: {geofence}", geofence);

        if (!_teslaMateMqttService.IsMqttClientConnected)
        {
            _logger.LogWarning("TeslaMate Mqtt Client is not connected. Charging Values won't be set.");
        }

        LogErrorForCarsWithUnknownSocLimit(_settings.CarsToManage);

        //Set to maximum current so will charge on full speed on auto wakeup
        foreach (var car in _settings.CarsToManage)
        {
            if (car.CarState is { IsHomeGeofence: true, State: CarStateEnum.Online }
                && car.CarState.ChargerRequestedCurrent != car.CarConfiguration.MaximumAmpere
                && car.CarConfiguration.ChargeMode != ChargeMode.DoNothing)
            {
                await _teslaService.SetAmp(car.Id, car.CarConfiguration.MaximumAmpere).ConfigureAwait(false);
            }
        }

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

        if (_configurationWrapper.LogLocationData())
        {
            _logger.LogDebug("Relevant cars: {@relevantCars}", relevantCars);
            _logger.LogDebug("Irrelevant cars: {@irrelevantCars}", irrelevantCars);
        }
        else
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new IgnorePropertiesResolver(new[] { nameof(DtoCar.CarState.Longitude), nameof(DtoCar.CarState.Latitude) }),
            };
            var relevantCarsJson = JsonConvert.SerializeObject(relevantCars, jsonSerializerSettings);
            _logger.LogDebug("Relevant cars: {relevantCarsJson}", relevantCarsJson);
            var irrelevantCarsJson = JsonConvert.SerializeObject(irrelevantCars, jsonSerializerSettings);
            _logger.LogDebug("Irrelevant cars: {irrelevantCarsJson}", irrelevantCarsJson);
        }
        

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

    private void SetAllPlannedChargingSlotsToInactive(DtoCar dtoCar)
    {
        foreach (var plannedChargingSlot in dtoCar.CarState.PlannedChargingSlots)
        {
            plannedChargingSlot.IsActive = false;
        }
    }

    private async Task UpdateChargingRelevantValues()
    {
        UpdateChargeTimes();
        await CalculateGeofences().ConfigureAwait(false);
        await _chargeTimeCalculationService.PlanChargeTimesForAllCars().ConfigureAwait(false);
        await _latestTimeToReachSocUpdateService.UpdateAllCars().ConfigureAwait(false);
    }

    private async Task CalculateGeofences()
    {
        _logger.LogTrace("{method}()", nameof(CalculateGeofences));
        var geofence = await _teslamateContext.Geofences
            .FirstOrDefaultAsync(g => g.Name == _configurationWrapper.GeoFence()).ConfigureAwait(false);
        if (geofence == null)
        {
            _logger.LogError("Specified geofence does not exist.");
            return;
        }
        foreach (var car in _settings.Cars)
        {
            if (car.CarState.Longitude == null || car.CarState.Latitude == null)
            {
                continue;
            }

            var distance = GetDistance(car.CarState.Longitude.Value, car.CarState.Latitude.Value,
                (double)geofence.Longitude, (double)geofence.Latitude);
            _logger.LogDebug("Calculated distance to home geofence for car {carId}: {calculatedDistance}", car.Id, distance);
            car.CarState.IsHomeGeofence = distance < geofence.Radius;
            car.CarState.DistanceToHomeGeofence = (int)distance - geofence.Radius;
        }
    }

    private double GetDistance(double longitude, double latitude, double otherLongitude, double otherLatitude)
    {
        var d1 = latitude * (Math.PI / 180.0);
        var num1 = longitude * (Math.PI / 180.0);
        var d2 = otherLatitude * (Math.PI / 180.0);
        var num2 = otherLongitude * (Math.PI / 180.0) - num1;
        var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) + Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);

        return 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
    }

    public int CalculateAmpByPowerAndCar(int powerToControl, DtoCar dtoCar)
    {
        _logger.LogTrace("{method}({powerToControl}, {carId})", nameof(CalculateAmpByPowerAndCar), powerToControl, dtoCar.Id);
        return Convert.ToInt32(Math.Floor(powerToControl / ((double)(_settings.AverageHomeGridVoltage ?? 230) * dtoCar.CarState.ActualPhases)));
    }

    public int CalculatePowerToControl(bool calculateAverage)
    {
        _logger.LogTrace("{method}({calculateAverage})", nameof(CalculatePowerToControl), calculateAverage);

        var buffer = _configurationWrapper.PowerBuffer(true);
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);
        var averagedOverage =
            calculateAverage ? _pvValueService.GetAveragedOverage() : (_settings.Overage ?? _constants.DefaultOverage);
        _logger.LogDebug("Averaged overage {averagedOverage}", averagedOverage);

        if (_configurationWrapper.FrontendConfiguration()?.GridValueSource == SolarValueSource.None
            && _configurationWrapper.FrontendConfiguration()?.InverterValueSource != SolarValueSource.None
            && _settings.InverterPower != null)
        {
            var chargingAtHomeSum = _settings.CarsToManage.Select(c => c.CarState.ChargingPowerAtHome).Sum();
            _logger.LogDebug("Using Inverter power {inverterPower} minus chargingPower at home {chargingPowerAtHome} as overage", _settings.InverterPower, chargingAtHomeSum);
            averagedOverage = _settings.InverterPower - chargingAtHomeSum ?? 0;
        }

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

    internal List<DtoCar> GetIrrelevantCars(List<int> relevantCarIds)
    {
        return _settings.Cars.Where(car => !relevantCarIds.Any(i => i == car.Id)).ToList();
    }

    private void LogErrorForCarsWithUnknownSocLimit(List<DtoCar> cars)
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

    private bool IsSocLimitUnknown(DtoCar dtoCar)
    {
        return dtoCar.CarState.SocLimit == null || dtoCar.CarState.SocLimit < _constants.MinSocLimit;
    }


    public List<int> GetRelevantCarIds()
    {
        var relevantIds = _settings.Cars
            .Where(c =>
                c.CarState.IsHomeGeofence == true
                && c.CarConfiguration.ShouldBeManaged == true
                && c.CarConfiguration.ChargeMode != ChargeMode.DoNothing
                //next line changed from == true to != false due to issue https://github.com/pkuehnel/TeslaSolarCharger/issues/365
                && c.CarState.PluggedIn != false
                && (c.CarState.ClimateOn == true ||
                    c.CarState.ChargerActualCurrent > 0 ||
                    c.CarState.SoC < (c.CarState.SocLimit - _constants.MinimumSocDifference)))
            .Select(c => c.Id)
            .ToList();

        return relevantIds;
    }

    /// <summary>
    /// Changes ampere of car
    /// </summary>
    /// <param name="dtoCar">car whose Ampere should be changed</param>
    /// <param name="ampToChange">Needed amp difference</param>
    /// <param name="maxAmpIncrease">Max Amp increase (also relevant for full speed charges)</param>
    /// <returns>Power difference</returns>
    private async Task<int> ChangeCarAmp(DtoCar dtoCar, int ampToChange, DtoValue<int> maxAmpIncrease)
    {
        _logger.LogTrace("{method}({param1}, {param2}, {param3})", nameof(ChangeCarAmp), dtoCar.Id, ampToChange, maxAmpIncrease.Value);
        if (maxAmpIncrease.Value < ampToChange)
        {
            _logger.LogDebug("Reduce current increase from {ampToChange}A to {maxAmpIncrease}A due to limited combined charging current.",
                ampToChange, maxAmpIncrease.Value);
            ampToChange = maxAmpIncrease.Value;
        }
        //This might happen if only climate is running or car nearly full which means full power is not needed.
        if (ampToChange > 0 && dtoCar.CarState.ChargerRequestedCurrent > dtoCar.CarState.ChargerActualCurrent && dtoCar.CarState.ChargerActualCurrent > 0)
        {
            //ampToChange = 0;
            _logger.LogWarning("Car does not use full request.");
        }
        var finalAmpsToSet = (dtoCar.CarState.ChargerRequestedCurrent ?? 0) + ampToChange;

        if (dtoCar.CarState.ChargerActualCurrent == 0)
        {
            finalAmpsToSet = (int)(dtoCar.CarState.ChargerActualCurrent + ampToChange);
        }

        _logger.LogDebug("Amps to set: {amps}", finalAmpsToSet);
        var ampChange = 0;
        var minAmpPerCar = dtoCar.CarConfiguration.MinimumAmpere;
        var maxAmpPerCar = dtoCar.CarConfiguration.MaximumAmpere;
        _logger.LogDebug("Min amp for car: {amp}", minAmpPerCar);
        _logger.LogDebug("Max amp for car: {amp}", maxAmpPerCar);
        await SendWarningOnChargerPilotReduced(dtoCar, maxAmpPerCar).ConfigureAwait(false);

        if (dtoCar.CarState.ChargerPilotCurrent != null)
        {
            if (minAmpPerCar > dtoCar.CarState.ChargerPilotCurrent)
            {
                minAmpPerCar = (int)dtoCar.CarState.ChargerPilotCurrent;
            }
            if (maxAmpPerCar > dtoCar.CarState.ChargerPilotCurrent)
            {
                maxAmpPerCar = (int)dtoCar.CarState.ChargerPilotCurrent;
            }
        }


        EnableFullSpeedChargeIfWithinPlannedChargingSlot(dtoCar);
        DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(dtoCar);

        //Falls MaxPower als Charge Mode: Leistung auf maximal
        if (dtoCar.CarConfiguration.ChargeMode == ChargeMode.MaxPower || dtoCar.CarState.AutoFullSpeedCharge)
        {
            _logger.LogDebug("Max Power Charging: ChargeMode: {chargeMode}, AutoFullSpeedCharge: {autofullspeedCharge}",
                dtoCar.CarConfiguration.ChargeMode, dtoCar.CarState.AutoFullSpeedCharge);
            if (dtoCar.CarState.ChargerRequestedCurrent != maxAmpPerCar || dtoCar.CarState.State != CarStateEnum.Charging || maxAmpIncrease.Value < 0)
            {
                var ampToSet = (maxAmpPerCar - dtoCar.CarState.ChargerRequestedCurrent) > maxAmpIncrease.Value ? ((dtoCar.CarState.ChargerActualCurrent ?? 0) + maxAmpIncrease.Value) : maxAmpPerCar;
                _logger.LogDebug("Set current to {ampToSet} after considering max car Current {maxAmpPerCar} and maxAmpIncrease {maxAmpIncrease}", ampToSet, maxAmpPerCar, maxAmpIncrease.Value);
                if (dtoCar.CarState.State != CarStateEnum.Charging)
                {
                    //Do not start charging when battery level near charge limit
                    if (dtoCar.CarState.SoC >=
                        dtoCar.CarState.SocLimit - _constants.MinimumSocDifference)
                    {
                        _logger.LogDebug("Do not start charging for car {carId} as set SoC Limit in your Tesla app needs to be 3% higher than actual SoC", dtoCar.Id);
                        return 0;
                    }
                    _logger.LogDebug("Charging schould start.");
                    await _teslaService.StartCharging(dtoCar.Id, ampToSet, dtoCar.CarState.State).ConfigureAwait(false);
                    ampChange += ampToSet - (dtoCar.CarState.ChargerActualCurrent ?? 0);
                }
                else
                {
                    await _teslaService.SetAmp(dtoCar.Id, ampToSet).ConfigureAwait(false);
                    ampChange += ampToSet - (dtoCar.CarState.ChargerActualCurrent ?? 0);
                }

            }

        }
        //Falls Laden beendet werden soll, aber noch ladend
        else if (finalAmpsToSet < minAmpPerCar && dtoCar.CarState.State == CarStateEnum.Charging)
        {
            _logger.LogDebug("Charging should stop");
            //Falls Ausschaltbefehl erst seit Kurzem
            if ((dtoCar.CarState.EarliestSwitchOff == default) || (dtoCar.CarState.EarliestSwitchOff > _dateTimeProvider.Now()))
            {
                _logger.LogDebug("Can not stop charging: earliest Switch Off: {earliestSwitchOff}",
                    dtoCar.CarState.EarliestSwitchOff);
                if (dtoCar.CarState.ChargerActualCurrent != minAmpPerCar)
                {
                    await _teslaService.SetAmp(dtoCar.Id, minAmpPerCar).ConfigureAwait(false);
                }
                ampChange += minAmpPerCar - (dtoCar.CarState.ChargerActualCurrent ?? 0);
            }
            //Laden Stoppen
            else
            {
                _logger.LogDebug("Stop Charging");
                await _teslaService.StopCharging(dtoCar.Id).ConfigureAwait(false);
                ampChange -= dtoCar.CarState.ChargerActualCurrent ?? 0;
            }
        }
        //Falls Laden beendet ist und beendet bleiben soll
        else if (finalAmpsToSet < minAmpPerCar)
        {
            _logger.LogDebug("Charging should stay stopped");
        }
        //Falls nicht ladend, aber laden soll beginnen
        else if (finalAmpsToSet >= minAmpPerCar && (dtoCar.CarState.State != CarStateEnum.Charging))
        {
            _logger.LogDebug("Charging should start");

            if (dtoCar.CarState.EarliestSwitchOn <= _dateTimeProvider.Now())
            {
                _logger.LogDebug("Charging is starting");
                var startAmp = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
                await _teslaService.StartCharging(dtoCar.Id, startAmp, dtoCar.CarState.State).ConfigureAwait(false);
                ampChange += startAmp;
            }
        }
        //Normal Ampere setzen
        else
        {
            _logger.LogDebug("Normal amp set");
            var ampToSet = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
            if (ampToSet != dtoCar.CarState.ChargerRequestedCurrent)
            {
                await _teslaService.SetAmp(dtoCar.Id, ampToSet).ConfigureAwait(false);
                ampChange += ampToSet - (dtoCar.CarState.ChargerActualCurrent ?? 0);
            }
            else
            {
                _logger.LogDebug("Current requested amp: {currentRequestedAmp} same as amp to set: {ampToSet} Do not change anything",
                    dtoCar.CarState.ChargerRequestedCurrent, ampToSet);
            }
        }

        maxAmpIncrease.Value -= ampChange;
        return ampChange * (dtoCar.CarState.ChargerVoltage ?? (_settings.AverageHomeGridVoltage ?? 230)) * dtoCar.CarState.ActualPhases;
    }

    private async Task SendWarningOnChargerPilotReduced(DtoCar dtoCar, int maxAmpPerCar)
    {
        if (dtoCar.CarState.ChargerPilotCurrent != null && maxAmpPerCar > dtoCar.CarState.ChargerPilotCurrent)
        {
            _logger.LogWarning("Charging speed of {carID} id reduced to {amp}", dtoCar.Id, dtoCar.CarState.ChargerPilotCurrent);
            if (!dtoCar.CarState.ReducedChargeSpeedWarning)
            {
                dtoCar.CarState.ReducedChargeSpeedWarning = true;
                await _telegramService
                    .SendMessage(
                        $"Charging of {dtoCar.CarState.Name} is reduced to {dtoCar.CarState.ChargerPilotCurrent} due to chargelimit of wallbox.")
                    .ConfigureAwait(false);
            }
        }
        else if (dtoCar.CarState.ReducedChargeSpeedWarning)
        {
            dtoCar.CarState.ReducedChargeSpeedWarning = false;
            await _telegramService.SendMessage($"Charging speed of {dtoCar.CarState.Name} is regained.").ConfigureAwait(false);
        }
    }

    internal void DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(DtoCar dtoCar)
    {
        var currentDate = _dateTimeProvider.DateTimeOffSetNow();
        var plannedChargeSlotInCurrentTime = dtoCar.CarState.PlannedChargingSlots
            .FirstOrDefault(c => c.ChargeStart <= currentDate && c.ChargeEnd > currentDate);
        if (plannedChargeSlotInCurrentTime == default)
        {
            dtoCar.CarState.AutoFullSpeedCharge = false;
            foreach (var plannedChargeSlot in dtoCar.CarState.PlannedChargingSlots)
            {
                plannedChargeSlot.IsActive = false;
            }
        }
    }

    internal void EnableFullSpeedChargeIfWithinPlannedChargingSlot(DtoCar dtoCar)
    {
        var currentDate = _dateTimeProvider.DateTimeOffSetNow();
        var plannedChargeSlotInCurrentTime = dtoCar.CarState.PlannedChargingSlots
            .FirstOrDefault(c => c.ChargeStart <= currentDate && c.ChargeEnd > currentDate);
        if (plannedChargeSlotInCurrentTime != default)
        {
            dtoCar.CarState.AutoFullSpeedCharge = true;
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

    private void UpdateShouldStartStopChargingSince(DtoCar dtoCar)
    {
        _logger.LogTrace("{method}({carId})", nameof(UpdateShouldStartStopChargingSince), dtoCar.Id);
        var powerToControl = CalculatePowerToControl(false);
        var ampToSet = CalculateAmpByPowerAndCar(powerToControl, dtoCar);
        _logger.LogTrace("Amp to set: {ampToSet}", ampToSet);
        if (dtoCar.CarState.IsHomeGeofence == true)
        {
            var actualCurrent = dtoCar.CarState.ChargerActualCurrent ?? 0;
            _logger.LogTrace("Actual current: {actualCurrent}", actualCurrent);
            //This is needed because sometimes actual current is higher than last set amp, leading to higher calculated amp to set, than actually needed
            var lastSetAmp = dtoCar.CarState.ChargerRequestedCurrent ?? dtoCar.CarState.LastSetAmp;
            if (actualCurrent > lastSetAmp)
            {
                _logger.LogTrace("Actual current {actualCurrent} higher than last set amp {lastSetAmp}. Setting actual current as last set amp.", actualCurrent, lastSetAmp);
                actualCurrent = lastSetAmp;
            }
            ampToSet += actualCurrent;
        }
        //Commented section not needed because should start should also be set if charging
        if (ampToSet >= dtoCar.CarConfiguration.MinimumAmpere/* && (car.CarState.ChargerActualCurrent is 0 or null)*/)
        {
            SetEarliestSwitchOnToNowWhenNotAlreadySet(dtoCar);
        }
        else
        {
            SetEarliestSwitchOffToNowWhenNotAlreadySet(dtoCar);
        }
    }

    internal void SetEarliestSwitchOnToNowWhenNotAlreadySet(DtoCar dtoCar)
    {
        _logger.LogTrace("{method}({param1})", nameof(SetEarliestSwitchOnToNowWhenNotAlreadySet), dtoCar.Id);
        if (dtoCar.CarState.ShouldStartChargingSince == null)
        {
            dtoCar.CarState.ShouldStartChargingSince = _dateTimeProvider.Now();
            var timespanUntilSwitchOn = _configurationWrapper.TimespanUntilSwitchOn();
            var earliestSwitchOn = dtoCar.CarState.ShouldStartChargingSince + timespanUntilSwitchOn;
            dtoCar.CarState.EarliestSwitchOn = earliestSwitchOn;
        }
        dtoCar.CarState.EarliestSwitchOff = null;
        dtoCar.CarState.ShouldStopChargingSince = null;
        _logger.LogDebug("Should start charging since: {shoudStartChargingSince}", dtoCar.CarState.ShouldStartChargingSince);
        _logger.LogDebug("Earliest switch on: {earliestSwitchOn}", dtoCar.CarState.EarliestSwitchOn);
    }

    internal void SetEarliestSwitchOffToNowWhenNotAlreadySet(DtoCar dtoCar)
    {
        _logger.LogTrace("{method}({param1})", nameof(SetEarliestSwitchOffToNowWhenNotAlreadySet), dtoCar.Id);
        if (dtoCar.CarState.ShouldStopChargingSince == null)
        {
            var currentDate = _dateTimeProvider.Now();
            _logger.LogTrace("Current date: {currentDate}", currentDate);
            dtoCar.CarState.ShouldStopChargingSince = currentDate;
            var timespanUntilSwitchOff = _configurationWrapper.TimespanUntilSwitchOff();
            _logger.LogTrace("TimeSpan until switch off: {timespanUntilSwitchOff}", timespanUntilSwitchOff);
            var earliestSwitchOff = dtoCar.CarState.ShouldStopChargingSince + timespanUntilSwitchOff;
            dtoCar.CarState.EarliestSwitchOff = earliestSwitchOff;
        }
        dtoCar.CarState.EarliestSwitchOn = null;
        dtoCar.CarState.ShouldStartChargingSince = null;
        _logger.LogDebug("Should start charging since: {shoudStopChargingSince}", dtoCar.CarState.ShouldStopChargingSince);
        _logger.LogDebug("Earliest switch off: {earliestSwitchOff}", dtoCar.CarState.EarliestSwitchOff);
    }



}
