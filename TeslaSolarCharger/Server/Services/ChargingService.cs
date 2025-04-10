using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Server.Services;

public class ChargingService(
    ILogger<ChargingService> logger,
    ISettings settings,
    IDateTimeProvider dateTimeProvider,
    ITelegramService telegramService,
    ITeslaService teslaService,
    IConfigurationWrapper configurationWrapper,
    ITeslaMateMqttService teslaMateMqttService,
    ILatestTimeToReachSocUpdateService latestTimeToReachSocUpdateService,
    IChargeTimeCalculationService chargeTimeCalculationService,
    IConstants constants,
    ITeslaSolarChargerContext context,
    IErrorHandlingService errorHandlingService,
    IIssueKeys issueKeys)
    : IChargingService
{

    public async Task SetNewChargingValues()
    {
        logger.LogTrace("{method}()", nameof(SetNewChargingValues));
        await UpdateChargingRelevantValues().ConfigureAwait(false);


        logger.LogDebug("Current overage is {overage} Watt.", settings.Overage);
        if (settings.Overage == null && settings.InverterPower == null)
        {
            logger.LogWarning("Can not control power as overage is unknown. Use int minValue");
        }
        var geofence = configurationWrapper.GeoFence();
        logger.LogDebug("Relevant Geofence: {geofence}", geofence);

        if (!teslaMateMqttService.IsMqttClientConnected)
        {
            logger.LogWarning("TeslaMate Mqtt Client is not connected. Charging Values won't be set.");
        }

        LogErrorForCarsWithUnknownSocLimit(settings.CarsToManage);

        var relevantCarIds = GetRelevantCarIds();
        logger.LogDebug("Relevant car ids: {@ids}", relevantCarIds);

        var irrelevantCars = GetIrrelevantCars(relevantCarIds);
        logger.LogDebug("Irrelevant car ids: {@ids}", irrelevantCars.Select(c => c.Id));
        foreach (var irrelevantCar in irrelevantCars)
        {
            SetAllPlannedChargingSlotsToInactive(irrelevantCar);
        }

        var relevantCars = settings.Cars
            .Where(c => relevantCarIds.Any(r => c.Id == r))
            .OrderBy(c => c.ChargingPriority)
            .ThenBy(c => c.Id)
            .ToList();

        if (configurationWrapper.LogLocationData())
        {
            logger.LogDebug("Relevant cars: {@relevantCars}", relevantCars);
            logger.LogDebug("Irrelevant cars: {@irrelevantCars}", irrelevantCars);
        }
        else
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new IgnorePropertiesResolver(new[] { nameof(DtoCar.Longitude), nameof(DtoCar.Latitude) }),
            };
            var relevantCarsJson = JsonConvert.SerializeObject(relevantCars, jsonSerializerSettings);
            logger.LogDebug("Relevant cars: {relevantCarsJson}", relevantCarsJson);
            var irrelevantCarsJson = JsonConvert.SerializeObject(irrelevantCars, jsonSerializerSettings);
            logger.LogDebug("Irrelevant cars: {irrelevantCarsJson}", irrelevantCarsJson);
        }

        var carsToSetToMaxCurrent = settings.CarsToManage
            .Where(c => c.State == CarStateEnum.Online
                        && c.IsHomeGeofence == true
                        && c.PluggedIn == true
                        && c.ChargerRequestedCurrent != c.MaximumAmpere)
            .ToList();

        foreach (var car in carsToSetToMaxCurrent)
        {
            await teslaService.SetAmp(car.Id, car.MaximumAmpere).ConfigureAwait(false);
        }
        

        if (relevantCarIds.Count < 1)
        {
            logger.LogDebug("No car was charging this cycle.");
            settings.ControlledACarAtLastCycle = false;
            return;
        }

        var powerToControl = await CalculatePowerToControl().ConfigureAwait(false);

        logger.LogDebug("At least one car is charging.");
        settings.ControlledACarAtLastCycle = true;

        logger.LogDebug("Power to control: {power}", powerToControl);

        var maxUsableCurrent = configurationWrapper.MaxCombinedCurrent();
        var currentlyUsedCurrent = relevantCars.Select(c => c.ChargerActualCurrent ?? 0).Sum();
        var maxAmpIncrease = new DtoValue<int>(maxUsableCurrent - currentlyUsedCurrent);

        if (powerToControl < 0 || maxAmpIncrease.Value < 0)
        {
            logger.LogTrace("Reversing car order");
            relevantCars.Reverse();
        }



        foreach (var relevantCar in relevantCars)
        {
            var requestedAmpChange = CalculateAmpByPowerAndCar(powerToControl, relevantCar);
            logger.LogDebug("Amp to control: {amp}", requestedAmpChange);
            logger.LogDebug("Update Car amp for car {carname}", relevantCar.Name);
            if (requestedAmpChange == 0)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.BleCommandNoSuccess + constants.SetChargingAmpsRequestUrl, relevantCar.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + constants.SetChargingAmpsRequestUrl, relevantCar.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.SetChargingAmpsRequestUrl, relevantCar.Vin);
            }
            powerToControl -= await ChangeCarAmp(relevantCar, requestedAmpChange, maxAmpIncrease).ConfigureAwait(false);
        }
    }

    private void SetAllPlannedChargingSlotsToInactive(DtoCar dtoCar)
    {
        foreach (var plannedChargingSlot in dtoCar.PlannedChargingSlots)
        {
            plannedChargingSlot.IsActive = false;
        }
    }

    private async Task UpdateChargingRelevantValues()
    {
        await UpdateChargeTimes();
        await CalculateGeofences();
        await chargeTimeCalculationService.PlanChargeTimesForAllCars().ConfigureAwait(false);
        await latestTimeToReachSocUpdateService.UpdateAllCars().ConfigureAwait(false);
    }

    private async Task CalculateGeofences()
    {
        logger.LogTrace("{method}()", nameof(CalculateGeofences));
        foreach (var car in settings.CarsToManage)
        {
            if (car.Longitude == null || car.Latitude == null)
            {
                logger.LogDebug("No location data for car {carId}. Do not calculate geofence", car.Id);
                car.DistanceToHomeGeofence = null;
                continue;
            }

            var homeDetectionVia = await context.Cars
                .Where(c => c.Id == car.Id)
                .Select(c => c.HomeDetectionVia)
                .FirstAsync();

            if (homeDetectionVia != HomeDetectionVia.GpsLocation)
            {
                logger.LogDebug("Car {carId} uses fleet telemetry but does not include tracking relevant fields. Do not calculate geofence", car.Id);
                car.DistanceToHomeGeofence = null;
                continue;
            }

            var distance = GetDistance(car.Longitude.Value, car.Latitude.Value,
            (double)configurationWrapper.HomeGeofenceLongitude(), (double)configurationWrapper.HomeGeofenceLatitude());
            logger.LogDebug("Calculated distance to home geofence for car {carId}: {calculatedDistance}", car.Id, distance);
            var radius = configurationWrapper.HomeGeofenceRadius();
            car.IsHomeGeofence = distance < radius;
            car.DistanceToHomeGeofence = (int)distance - radius;
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
        logger.LogTrace("{method}({powerToControl}, {carId})", nameof(CalculateAmpByPowerAndCar), powerToControl, dtoCar.Id);
        return Convert.ToInt32(Math.Floor(powerToControl / ((double)(settings.AverageHomeGridVoltage ?? 230) * dtoCar.ActualPhases)));
    }

    public async Task<int> CalculatePowerToControl()
    {
        logger.LogTrace("{method}()", nameof(CalculatePowerToControl));

        var buffer = configurationWrapper.PowerBuffer();
        logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);
        var averagedOverage = settings.Overage ?? constants.DefaultOverage;
        logger.LogDebug("Averaged overage {averagedOverage}", averagedOverage);

        var resultConfigurations = await context.ModbusResultConfigurations.Select(r => r.UsedFor).ToListAsync();
        resultConfigurations.AddRange(await context.RestValueResultConfigurations.Select(r => r.UsedFor).ToListAsync());
        resultConfigurations.AddRange(await context.MqttResultConfigurations.Select(r => r.UsedFor).ToListAsync());
        if ((!resultConfigurations.Any(r => r == ValueUsage.GridPower))
            && resultConfigurations.Any(r => r == ValueUsage.InverterPower)
            && settings.InverterPower != null)
        {
            var chargingAtHomeSum = settings.CarsToManage.Select(c => c.ChargingPowerAtHome).Sum();
            logger.LogDebug("Using Inverter power {inverterPower} minus chargingPower at home {chargingPowerAtHome} as overage", settings.InverterPower, chargingAtHomeSum);
            averagedOverage = settings.InverterPower.Value - (chargingAtHomeSum ?? 0);
        }

        var overage = averagedOverage - buffer;
        logger.LogDebug("Overage after subtracting power buffer ({buffer}): {overage}", buffer, overage);

        overage = AddHomeBatteryStateToPowerCalculation(overage);

        var powerToControl = overage;
        return powerToControl;
    }

    internal int AddHomeBatteryStateToPowerCalculation(int overage)
    {
        var homeBatteryMinSoc = configurationWrapper.HomeBatteryMinSoc();
        logger.LogDebug("Home battery min soc: {homeBatteryMinSoc}", homeBatteryMinSoc);
        var homeBatteryMaxChargingPower = configurationWrapper.HomeBatteryChargingPower();
        logger.LogDebug("Home battery should charging power: {homeBatteryMaxChargingPower}", homeBatteryMaxChargingPower);
        if (homeBatteryMinSoc != null && homeBatteryMaxChargingPower != null)
        {
            var actualHomeBatterySoc = settings.HomeBatterySoc;
            logger.LogDebug("Home battery actual soc: {actualHomeBatterySoc}", actualHomeBatterySoc);
            var actualHomeBatteryPower = settings.HomeBatteryPower;
            logger.LogDebug("Home battery actual power: {actualHomeBatteryPower}", actualHomeBatteryPower);
            if (actualHomeBatterySoc != null && actualHomeBatteryPower != null)
            {
                var batteryMinChargingPower = GetBatteryTargetChargingPower();
                var overageToIncrease = actualHomeBatteryPower.Value - batteryMinChargingPower;
                overage += overageToIncrease;
                var inverterAcOverload = (configurationWrapper.MaxInverterAcPower() - settings.InverterPower) * (-1);
                if (inverterAcOverload > 0)
                {
                    logger.LogDebug("As inverter power is higher than max inverter AC power, overage is reduced by overload");
                    overage -= (inverterAcOverload.Value - batteryMinChargingPower);
                }
            }
        }

        return overage;
    }

    public int GetBatteryTargetChargingPower()
    {
        var actualHomeBatterySoc = settings.HomeBatterySoc;
        var homeBatteryMinSoc = configurationWrapper.HomeBatteryMinSoc();
        var homeBatteryMaxChargingPower = configurationWrapper.HomeBatteryChargingPower();
        if (actualHomeBatterySoc < homeBatteryMinSoc)
        {
            return homeBatteryMaxChargingPower ?? 0;
        }

        return 0;
    }

    internal List<DtoCar> GetIrrelevantCars(List<int> relevantCarIds)
    {
        return settings.Cars.Where(car => !relevantCarIds.Any(i => i == car.Id)).ToList();
    }

    private void LogErrorForCarsWithUnknownSocLimit(List<DtoCar> cars)
    {
        foreach (var car in cars)
        {
            var unknownSocLimit = IsSocLimitUnknown(car);
            if (unknownSocLimit &&
                (car.State == null ||
                 car.State == CarStateEnum.Unknown ||
                 car.State == CarStateEnum.Asleep ||
                 car.State == CarStateEnum.Offline))
            {
                logger.LogWarning("Unknown charge limit of car {carId}.", car.Id);
            }
        }
    }

    private bool IsSocLimitUnknown(DtoCar dtoCar)
    {
        return dtoCar.SocLimit == null || dtoCar.SocLimit < constants.MinSocLimit;
    }


    public List<int> GetRelevantCarIds()
    {
        var relevantIds = settings.Cars
            .Where(c =>
                c.IsHomeGeofence == true
                && c.ShouldBeManaged == true
                && c.ChargeMode != ChargeMode.DoNothing
                //next line changed from == true to != false due to issue https://github.com/pkuehnel/TeslaSolarCharger/issues/365
                && c.PluggedIn != false
                && (c.ChargerActualCurrent > 0 ||
                    (c.SoC < (c.SocLimit - constants.MinimumSocDifference))))
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
        logger.LogTrace("{method}({param1}, {param2}, {param3})", nameof(ChangeCarAmp), dtoCar.Id, ampToChange, maxAmpIncrease.Value);
        var actualCurrent = dtoCar.ChargerActualCurrent;
        if (maxAmpIncrease.Value < ampToChange)
        {
            logger.LogDebug("Reduce current increase from {ampToChange}A to {maxAmpIncrease}A due to limited combined charging current.",
                ampToChange, maxAmpIncrease.Value);
            ampToChange = maxAmpIncrease.Value;
        }
        //This might happen if only climate is running or car nearly full which means full power is not needed.
        if (ampToChange > 0 && dtoCar.ChargerRequestedCurrent > actualCurrent && actualCurrent > 0)
        {
            //ampToChange = 0;
            logger.LogWarning("Car does not use full request.");
        }
        var finalAmpsToSet = (dtoCar.ChargerRequestedCurrent ?? 0) + ampToChange;

        if (actualCurrent == 0)
        {
            finalAmpsToSet = (int)(actualCurrent + ampToChange);
        }

        logger.LogDebug("Amps to set: {amps}", finalAmpsToSet);
        var ampChange = 0;
        var minAmpPerCar = dtoCar.MinimumAmpere;
        var maxAmpPerCar = dtoCar.MaximumAmpere;
        logger.LogDebug("Min amp for car: {amp}", minAmpPerCar);
        logger.LogDebug("Max amp for car: {amp}", maxAmpPerCar);
        await SendWarningOnChargerPilotReduced(dtoCar, maxAmpPerCar).ConfigureAwait(false);

        if (dtoCar.ChargerPilotCurrent != null)
        {
            if (minAmpPerCar > dtoCar.ChargerPilotCurrent)
            {
                minAmpPerCar = (int)dtoCar.ChargerPilotCurrent;
            }
            if (maxAmpPerCar > dtoCar.ChargerPilotCurrent)
            {
                maxAmpPerCar = (int)dtoCar.ChargerPilotCurrent;
            }
        }


        EnableFullSpeedChargeIfWithinPlannedChargingSlot(dtoCar);
        DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(dtoCar);

        //Falls MaxPower als Charge Mode: Leistung auf maximal
        if (dtoCar.ChargeMode == ChargeMode.MaxPower || dtoCar.AutoFullSpeedCharge)
        {
            logger.LogDebug("Max Power Charging: ChargeMode: {chargeMode}, AutoFullSpeedCharge: {autofullspeedCharge}",
                dtoCar.ChargeMode, dtoCar.AutoFullSpeedCharge);
            if (dtoCar.ChargerRequestedCurrent != maxAmpPerCar || dtoCar.State != CarStateEnum.Charging || maxAmpIncrease.Value < 0)
            {
                var ampToSet = (maxAmpPerCar - dtoCar.ChargerRequestedCurrent) > maxAmpIncrease.Value ? ((actualCurrent ?? 0) + maxAmpIncrease.Value) : maxAmpPerCar;
                logger.LogDebug("Set current to {ampToSet} after considering max car Current {maxAmpPerCar} and maxAmpIncrease {maxAmpIncrease}", ampToSet, maxAmpPerCar, maxAmpIncrease.Value);
                if (dtoCar.State != CarStateEnum.Charging)
                {
                    //Do not start charging when battery level near charge limit
                    if (dtoCar.SoC >=
                        dtoCar.SocLimit - constants.MinimumSocDifference)
                    {
                        logger.LogDebug("Do not start charging for car {carId} as set SoC Limit in your Tesla app needs to be 3% higher than actual SoC", dtoCar.Id);
                        return 0;
                    }
                    logger.LogDebug("Charging schould start.");
                    await teslaService.StartCharging(dtoCar.Id, ampToSet, dtoCar.State).ConfigureAwait(false);
                    ampChange += ampToSet - (actualCurrent ?? 0);
                }
                else
                {
                    await teslaService.SetAmp(dtoCar.Id, ampToSet).ConfigureAwait(false);
                    ampChange += ampToSet - (actualCurrent ?? 0);
                }

            }

        }
        //Falls Laden beendet werden soll, aber noch ladend
        else if (finalAmpsToSet < minAmpPerCar && dtoCar.State == CarStateEnum.Charging)
        {
            logger.LogDebug("Charging should stop");
            //Falls Ausschaltbefehl erst seit Kurzem
            if ((dtoCar.EarliestSwitchOff == default) || (dtoCar.EarliestSwitchOff > dateTimeProvider.Now()))
            {
                logger.LogDebug("Can not stop charging: earliest Switch Off: {earliestSwitchOff}",
                    dtoCar.EarliestSwitchOff);
                if (actualCurrent != minAmpPerCar)
                {
                    await teslaService.SetAmp(dtoCar.Id, minAmpPerCar).ConfigureAwait(false);
                }
                ampChange += minAmpPerCar - (actualCurrent ?? 0);
            }
            //Laden Stoppen
            else
            {
                logger.LogDebug("Stop Charging");
                await teslaService.StopCharging(dtoCar.Id).ConfigureAwait(false);
                ampChange -= actualCurrent ?? 0;
            }
        }
        //Falls Laden beendet ist und beendet bleiben soll
        else if (finalAmpsToSet < minAmpPerCar)
        {
            logger.LogDebug("Charging should stay stopped");
        }
        //Falls nicht ladend, aber laden soll beginnen
        else if (finalAmpsToSet >= minAmpPerCar && (dtoCar.State != CarStateEnum.Charging))
        {
            logger.LogDebug("Charging should start");

            if (dtoCar.EarliestSwitchOn <= dateTimeProvider.Now())
            {
                logger.LogDebug("Charging is starting");
                var startAmp = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
                await teslaService.StartCharging(dtoCar.Id, startAmp, dtoCar.State).ConfigureAwait(false);
                ampChange += startAmp;
            }
        }
        //Normal Ampere setzen
        else
        {
            logger.LogDebug("Normal amp set");
            var ampToSet = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
            if (ampToSet != dtoCar.ChargerRequestedCurrent)
            {
                await teslaService.SetAmp(dtoCar.Id, ampToSet).ConfigureAwait(false);
                ampChange += ampToSet - (actualCurrent ?? 0);
            }
            else
            {
                logger.LogDebug("Current requested amp: {currentRequestedAmp} same as amp to set: {ampToSet} Do not change anything",
                    dtoCar.ChargerRequestedCurrent, ampToSet);
            }
        }

        maxAmpIncrease.Value -= ampChange;
        return ampChange * (dtoCar.ChargerVoltage ?? (settings.AverageHomeGridVoltage ?? 230)) * dtoCar.ActualPhases;
    }

    private async Task SendWarningOnChargerPilotReduced(DtoCar dtoCar, int maxAmpPerCar)
    {
        if (dtoCar.ChargerPilotCurrent != null && maxAmpPerCar > dtoCar.ChargerPilotCurrent)
        {
            logger.LogWarning("Charging speed of {carID} id reduced to {amp}", dtoCar.Id, dtoCar.ChargerPilotCurrent);
            if (!dtoCar.ReducedChargeSpeedWarning)
            {
                dtoCar.ReducedChargeSpeedWarning = true;
                await telegramService
                    .SendMessage(
                        $"Charging of {dtoCar.Name} is reduced to {dtoCar.ChargerPilotCurrent} due to chargelimit of wallbox.")
                    .ConfigureAwait(false);
            }
        }
        else if (dtoCar.ReducedChargeSpeedWarning)
        {
            dtoCar.ReducedChargeSpeedWarning = false;
            await telegramService.SendMessage($"Charging speed of {dtoCar.Name} is regained.").ConfigureAwait(false);
        }
    }

    internal void DisableFullSpeedChargeIfWithinNonePlannedChargingSlot(DtoCar dtoCar)
    {
        var currentDate = dateTimeProvider.DateTimeOffSetNow();
        var plannedChargeSlotInCurrentTime = dtoCar.PlannedChargingSlots
            .FirstOrDefault(c => c.ChargeStart <= currentDate && c.ChargeEnd > currentDate);
        if (plannedChargeSlotInCurrentTime == default)
        {
            dtoCar.AutoFullSpeedCharge = false;
            foreach (var plannedChargeSlot in dtoCar.PlannedChargingSlots)
            {
                plannedChargeSlot.IsActive = false;
            }
        }
    }

    internal void EnableFullSpeedChargeIfWithinPlannedChargingSlot(DtoCar dtoCar)
    {
        var currentDate = dateTimeProvider.DateTimeOffSetNow();
        var plannedChargeSlotInCurrentTime = dtoCar.PlannedChargingSlots
            .FirstOrDefault(c => c.ChargeStart <= currentDate && c.ChargeEnd > currentDate);
        if (plannedChargeSlotInCurrentTime != default)
        {
            dtoCar.AutoFullSpeedCharge = true;
            plannedChargeSlotInCurrentTime.IsActive = true;
        }
    }

    private async Task UpdateChargeTimes()
    {
        logger.LogTrace("{method}()", nameof(UpdateChargeTimes));
        foreach (var car in settings.CarsToManage)
        {
            chargeTimeCalculationService.UpdateChargeTime(car);
            await UpdateShouldStartStopChargingSince(car).ConfigureAwait(false);
        }
    }

    private async Task UpdateShouldStartStopChargingSince(DtoCar dtoCar)
    {
        logger.LogTrace("{method}({carId})", nameof(UpdateShouldStartStopChargingSince), dtoCar.Id);
        var powerToControl = await CalculatePowerToControl().ConfigureAwait(false);
        var ampToSet = CalculateAmpByPowerAndCar(powerToControl, dtoCar);
        logger.LogTrace("Amp to set: {ampToSet}", ampToSet);
        if (dtoCar.IsHomeGeofence == true)
        {
            var actualCurrent = dtoCar.ChargerActualCurrent ?? 0;
            logger.LogTrace("Actual current: {actualCurrent}", actualCurrent);
            //This is needed because sometimes actual current is higher than last set amp, leading to higher calculated amp to set, than actually needed
            var lastSetAmp = dtoCar.ChargerRequestedCurrent ?? dtoCar.LastSetAmp;
            if (actualCurrent > lastSetAmp)
            {
                logger.LogTrace("Actual current {actualCurrent} higher than last set amp {lastSetAmp}. Setting actual current as last set amp.", actualCurrent, lastSetAmp);
                actualCurrent = lastSetAmp;
            }
            ampToSet += actualCurrent;
        }
        //Commented section not needed because should start should also be set if charging
        if (ampToSet >= dtoCar.MinimumAmpere/* && (car.CarState.ChargerActualCurrent is 0 or null)*/)
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
        logger.LogTrace("{method}({param1})", nameof(SetEarliestSwitchOnToNowWhenNotAlreadySet), dtoCar.Id);
        if (dtoCar.ShouldStartChargingSince == null)
        {
            dtoCar.ShouldStartChargingSince = dateTimeProvider.Now();
            var timespanUntilSwitchOn = configurationWrapper.TimespanUntilSwitchOn();
            var earliestSwitchOn = dtoCar.ShouldStartChargingSince + timespanUntilSwitchOn;
            dtoCar.EarliestSwitchOn = earliestSwitchOn;
        }
        dtoCar.EarliestSwitchOff = null;
        dtoCar.ShouldStopChargingSince = null;
        logger.LogDebug("Should start charging since: {shoudStartChargingSince}", dtoCar.ShouldStartChargingSince);
        logger.LogDebug("Earliest switch on: {earliestSwitchOn}", dtoCar.EarliestSwitchOn);
    }

    internal void SetEarliestSwitchOffToNowWhenNotAlreadySet(DtoCar dtoCar)
    {
        logger.LogTrace("{method}({param1})", nameof(SetEarliestSwitchOffToNowWhenNotAlreadySet), dtoCar.Id);
        if (dtoCar.ShouldStopChargingSince == null)
        {
            var currentDate = dateTimeProvider.Now();
            logger.LogTrace("Current date: {currentDate}", currentDate);
            dtoCar.ShouldStopChargingSince = currentDate;
            var timespanUntilSwitchOff = configurationWrapper.TimespanUntilSwitchOff();
            logger.LogTrace("TimeSpan until switch off: {timespanUntilSwitchOff}", timespanUntilSwitchOff);
            var earliestSwitchOff = dtoCar.ShouldStopChargingSince + timespanUntilSwitchOff;
            dtoCar.EarliestSwitchOff = earliestSwitchOff;
        }
        dtoCar.EarliestSwitchOn = null;
        dtoCar.ShouldStartChargingSince = null;
        logger.LogDebug("Should start charging since: {shoudStopChargingSince}", dtoCar.ShouldStopChargingSince);
        logger.LogDebug("Earliest switch off: {earliestSwitchOff}", dtoCar.EarliestSwitchOff);
    }



}
