using System.Text;
using Newtonsoft.Json;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.Enums;
using Car = SmartTeslaAmpSetter.Shared.Dtos.Settings.Car;

namespace SmartTeslaAmpSetter.Server.Services;

public class ChargingService
{
    private readonly ILogger<ChargingService> _logger;
    private readonly GridService _gridService;
    private readonly IConfiguration _configuration;
    private readonly Settings _settings;
    private readonly string _teslaMateBaseUrl;

    public ChargingService(ILogger<ChargingService> logger, GridService gridService, IConfiguration configuration, Settings settings)
    {
        _logger = logger;
        _gridService = gridService;
        _configuration = configuration;
        _settings = settings;
        _teslaMateBaseUrl = _configuration.GetValue<string>("TeslaMateApiBaseUrl");
    }

    public async Task SetNewChargingValues(bool onlyUpdateValues = false)
    {
        _logger.LogTrace($"{nameof(SetNewChargingValues)}()");

        var overage = await _gridService.GetCurrentOverage().ConfigureAwait(false);

        _settings.Overage = overage;

        _logger.LogDebug($"Current overage is {overage} Watt.");

        var inverterPower = await _gridService.GetCurrentInverterPower().ConfigureAwait(false);

        _settings.InverterPower = inverterPower;

        _logger.LogDebug($"Current overage is {overage} Watt.");

        var buffer = _configuration.GetValue<int>("PowerBuffer");
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);

        overage -= buffer;

        var geofence = _configuration.GetValue<string>("GeoFence");
        _logger.LogDebug("Relevant Geofence: {geofence}", geofence);

        foreach (var car in _settings.Cars)
        {
            if (car.CarState.SocLimit == null)
            {
                _logger.LogWarning("Unknown charge limit of car {carId}. Waking up car.", car.Id);
                await WakeUpCar(car.Id).ConfigureAwait(false);
            }
        }

        var relevantCarIds = GetRelevantCarIds(geofence);
        _logger.LogDebug("Number of relevant Cars: {count}", relevantCarIds.Count);

        var relevantCars = _settings.Cars.Where(c => relevantCarIds.Any(r => c.Id == r)).ToList();

        foreach (var relevantCar in relevantCars)
        {
            relevantCar.CarState.ChargingPowerAtHome = relevantCar.CarState.ChargingPower;
        }

        foreach (var irrelevantCar in _settings.Cars
                     .Where(c => c.CarState.PluggedIn != true).ToList())
        {
            _logger.LogDebug("Resetting ChargeStart and ChargeStop for car {carId}", irrelevantCar.Id);
            UpdateEarliestTimesAfterSwitch(irrelevantCar.Id);
            irrelevantCar.CarState.ChargingPowerAtHome = 0;
        }

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



    private List<int> GetRelevantCarIds(string geofence)
    {
        var relevantIds = _settings.Cars
            .Where(c =>
                c.CarState.Geofence == geofence
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

        var activePhases = relevantCar.CarState.ChargerPhases > 1 ? 3 : 1;
        var reachedMinimumSocAtFullSpeedChargeDateTime = ReachedMinimumSocAtFullSpeedChargeDateTime(relevantCar, activePhases);

        //FullSpeed Aktivieren, wenn Minimum Soc nicht mehr erreicht werden kann
        if (reachedMinimumSocAtFullSpeedChargeDateTime > relevantCar.CarConfiguration.LatestTimeToReachSoC 
            && relevantCar.CarConfiguration.LatestTimeToReachSoC > DateTime.Now 
            || relevantCar.CarState.SoC < relevantCar.CarConfiguration.MinimumSoC && relevantCar.CarConfiguration.ChargeMode == ChargeMode.PvAndMinSoc)
        {
            relevantCar.CarState.AutoFullSpeedCharge = true;
        }
        //FullSpeed deaktivieren, wenn Minimum Soc erreicht wurde, oder Ziel SoC mehr als eine halbe Stunde zu früh erreicht
        if (relevantCar.CarState.AutoFullSpeedCharge && 
            (relevantCar.CarState.SoC >= relevantCar.CarConfiguration.MinimumSoC || reachedMinimumSocAtFullSpeedChargeDateTime < relevantCar.CarConfiguration.LatestTimeToReachSoC.AddMinutes(-30)))
        {
            relevantCar.CarState.AutoFullSpeedCharge = false;
        }

        //Falls MaxPower als Charge Mode: Leistung auf maximal
        if (relevantCar.CarConfiguration.ChargeMode == ChargeMode.MaxPower || relevantCar.CarState.AutoFullSpeedCharge)
        {
            _logger.LogDebug("Max Power Charging");
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
                    await StartCharging(relevantCar.Id, ampToSet, relevantCar.CarState.State).ConfigureAwait(false);
                    ampChange += ampToSet - (relevantCar.CarState.ChargerActualCurrent?? 0);
                    UpdateEarliestTimesAfterSwitch(relevantCar.Id);
                }
                else
                {
                    await SetAmp(relevantCar.Id, ampToSet).ConfigureAwait(false);
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
                    await SetAmp(relevantCar.Id, minAmpPerCar).ConfigureAwait(false);
                }
                ampChange += minAmpPerCar - (relevantCar.CarState.ChargerActualCurrent?? 0);
            }
            //Laden Stoppen
            else
            {
                _logger.LogDebug("Stop Charging");
                await StopCharging(relevantCar.Id).ConfigureAwait(false);
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
        else if (finalAmpsToSet > minAmpPerCar && relevantCar.CarState.ChargerActualCurrent == 0)
        {
            _logger.LogDebug("Charging should start");
            var earliestSwitchOn = EarliestSwitchOn(relevantCar.Id);

            if (earliestSwitchOn <= DateTime.Now)
            {
                _logger.LogDebug("Charging should start");
                var startAmp = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
                await StartCharging(relevantCar.Id, startAmp, relevantCar.CarState.State).ConfigureAwait(false);
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
                await SetAmp(relevantCar.Id, ampToSet).ConfigureAwait(false);
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

    private static DateTime ReachedMinimumSocAtFullSpeedChargeDateTime(Car car, int numberOfPhases)
    {
        var socToCharge = (double) car.CarConfiguration.MinimumSoC - (car.CarState.SoC ?? 0);
        if (socToCharge < 1)
        {
            return DateTime.Now + TimeSpan.Zero;
        }
        var energyToCharge = car.CarConfiguration.UsableEnergy * 1000 * (decimal) (socToCharge / 100.0);
        var maxChargingPower =
            car.CarConfiguration.MaximumAmpere * numberOfPhases
                //Use 230 instead of actual voltage because of 0 Volt if charging is stopped
                * 230;
        return DateTime.Now + TimeSpan.FromHours((double) (energyToCharge/maxChargingPower));
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
        var minutesUntilSwitchOff = _configuration.GetValue<int>("MinutesUntilSwitchOff");
        var car = _settings.Cars.First(c => c.Id == carId);
        if (car.CarState.ShouldStopChargingSince == DateTime.MaxValue)
        {
            car.CarState.ShouldStopChargingSince = DateTime.Now.AddMinutes(minutesUntilSwitchOff);
        }

        var earliestSwitchOff = car.CarState.ShouldStopChargingSince;
        return earliestSwitchOff;
    }

    private DateTime EarliestSwitchOn(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(EarliestSwitchOn), carId);
        var minutesUntilSwitchOn = _configuration.GetValue<int>("MinutesUntilSwitchOn");
        var car = _settings.Cars.First(c => c.Id == carId);
        if (car.CarState.ShouldStartChargingSince == DateTime.MaxValue)
        {
            car.CarState.ShouldStartChargingSince = DateTime.Now.AddMinutes(minutesUntilSwitchOn);
        }

        var earliestSwitchOn = car.CarState.ShouldStartChargingSince;
        return earliestSwitchOn;
    }

    private async Task StartCharging(int carId, int startAmp, string? carState)
    {
        _logger.LogTrace("{method}({param1}, {param2}, {param3})", nameof(StartCharging), carId, startAmp, carState);

        if (carState != null && (carState.Equals("offline", StringComparison.CurrentCultureIgnoreCase) ||
                                 carState.Equals("asleep", StringComparison.CurrentCultureIgnoreCase)))
        {
            _logger.LogInformation("Wakeup car before charging");
            await WakeUpCar(carId);
        }

        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/charge_start";

        var result = await SendPostToTeslaMate(url).ConfigureAwait(false);

        await ResumeLogging(carId);

        await SetAmp(carId, startAmp).ConfigureAwait(false);

        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);
    }

    private async Task WakeUpCar(int carId)
    {
        _logger.LogTrace("{method}({param})", nameof(WakeUpCar), carId);

        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/wake_up";

        var result = await SendPostToTeslaMate(url).ConfigureAwait(false);
        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);

        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    private async Task ResumeLogging(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(ResumeLogging), carId);
        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/logging/resume";
        using var httpClient = new HttpClient();
        var response = await httpClient.PutAsync(url, null);
        response.EnsureSuccessStatusCode();
    }

    private async Task StopCharging(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(StopCharging), carId);
        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/charge_stop";

        var result = await SendPostToTeslaMate(url).ConfigureAwait(false);

        var car = _settings.Cars.First(c => c.Id == carId);
        car.CarState.LastSetAmp = 0;

        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);
    }

    private async Task SetAmp(int carId, int amps)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SetAmp), carId, amps);
        var car = _settings.Cars.First(c => c.Id == carId);
        var parameters = new Dictionary<string, string>()
            {
                {"charging_amps", amps.ToString()},
            };

        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/set_charging_amps";

        var result = await SendPostToTeslaMate(url, parameters).ConfigureAwait(false);

        if (amps < 5)
        {
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            result = await SendPostToTeslaMate(url, parameters).ConfigureAwait(false);
        }

        car.CarState.LastSetAmp = amps;

        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);
    }

    private async Task<HttpResponseMessage> SendPostToTeslaMate(string url, Dictionary<string, string>? parameters = null)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SendPostToTeslaMate), url, parameters);
        var jsonString = JsonConvert.SerializeObject(parameters);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(url, content).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error while sending post to TeslaMate. Response: {response}", response.Content.ReadAsStringAsync());
        }
        response.EnsureSuccessStatusCode();
        return response;
    }
}