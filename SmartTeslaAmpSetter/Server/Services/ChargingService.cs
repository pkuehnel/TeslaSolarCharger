using System.Text;
using Newtonsoft.Json;
using SmartTeslaAmpSetter.Server.Dtos;
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

        var carIds = _settings.Cars.Select(c => c.Id).ToList();

        var teslaMateStates = await GetTeslaMateStates(carIds).ConfigureAwait(false);
        
        var geofence = _configuration.GetValue<string>("GeoFence");
        _logger.LogDebug("Relevant Geofence: {geofence}", geofence);

        UpdateCarStates(teslaMateStates, geofence);

        if (onlyUpdateValues)
        {
            return;
        }

        var relevantTeslaMateStates = GetRelevantTeslaMateStates(teslaMateStates, geofence);
        _logger.LogDebug("Number of relevant Cars: {count}", relevantTeslaMateStates.Count);

        foreach (var irrelevantTeslaMateState in teslaMateStates
                     .Where(t => !t.data.status.charging_details.plugged_in).ToList())
        {
            _logger.LogDebug("Resetting ChargeStart and ChargeStop for car {carId}", irrelevantTeslaMateState.data.car.car_id);
            UpdateEarliestTimesAfterSwitch(irrelevantTeslaMateState.data.car.car_id);
        }

        if (relevantTeslaMateStates.Count < 1)
        {
            return;
        }

        var currentRegulatedPower = relevantTeslaMateStates
            .Sum(relevantTeslaMateState => relevantTeslaMateState.data.status.charging_details.ChargingPower);
        _logger.LogDebug("Current regulated Power: {power}", currentRegulatedPower);

        var powerToRegulate = overage;
        _logger.LogDebug("Power to regulate: {power}", powerToRegulate);

        var ampToRegulate = Convert.ToInt32(Math.Floor(powerToRegulate / ((double)230 * 3)));
        _logger.LogDebug("Amp to regulate: {amp}", ampToRegulate);

        var orderedRelevantTeslaMateStates = relevantTeslaMateStates;
        if (ampToRegulate < 0)
        {
            _logger.LogDebug("Reversing car order");
            orderedRelevantTeslaMateStates.Reverse();
        }

        foreach (var releventTeslaMateState in orderedRelevantTeslaMateStates)
        {
            _logger.LogDebug("Update Car amp for car {carname}", releventTeslaMateState.data.car.car_name);
            ampToRegulate -= await ChangeCarAmp(releventTeslaMateState, ampToRegulate).ConfigureAwait(false);
        }
    }



    private static List<TeslaMateState> GetRelevantTeslaMateStates(List<TeslaMateState> teslaMateStates, string geofence)
    {
        var relevantTeslaMateStates = teslaMateStates
            .Where(t =>
                t.data.status.car_geodata.geofence == geofence
                && t.data.status.charging_details.plugged_in
                && (t.data.status.climate_details.is_climate_on ||
                    t.data.status.charging_details.charger_actual_current > 0 ||
                    t.data.status.battery_details.battery_level <
                    t.data.status.charging_details.charge_limit_soc - 2))
            .ToList();
        return relevantTeslaMateStates;
    }



    private void UpdateCarStates(List<TeslaMateState> teslaMateStates, string geofence)
    {
        foreach (var teslaMateState in teslaMateStates)
        {
            var car = _settings.Cars.First(c => c.Id == teslaMateState.data.car.car_id);
            car.CarState.Name = teslaMateState.data.car.car_name;
            car.CarState.Geofence = teslaMateState.data.status.car_geodata.geofence;
            car.CarState.SoC = teslaMateState.data.status.battery_details.battery_level;
            car.CarState.SocLimit = teslaMateState.data.status.charging_details.charge_limit_soc;
            car.CarState.TimeUntilFullCharge =
                TimeSpan.FromHours(teslaMateState.data.status.charging_details.time_to_full_charge);

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (teslaMateState.data.status.car_geodata.geofence.Equals(geofence,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                car.CarState.ChargingPowerAtHome = teslaMateState.data.status.charging_details.ChargingPower;
            }
            else
            {
                car.CarState.ChargingPowerAtHome = 0;
            }
        }
    }

    private async Task<int> ChangeCarAmp(TeslaMateState teslaMateState, int ampToRegulate)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(ChangeCarAmp), teslaMateState.data.car.car_name, ampToRegulate);
        var finalAmpsToSet = teslaMateState.data.status.charging_details.charger_actual_current + ampToRegulate;
        _logger.LogDebug("Amps to set: {amps}", finalAmpsToSet);
        var car = _settings.Cars.First(c => c.Id == teslaMateState.data.car.car_id);
        var ampChange = 0;
        var minAmpPerCar = car.CarConfiguration.MinimumAmpere;
        var maxAmpPerCar = car.CarConfiguration.MaximumAmpere;
        _logger.LogDebug("Min amp for car: {amp}", minAmpPerCar);
        _logger.LogDebug("Max amp for car: {amp}", maxAmpPerCar);

        
        var reachedMinimumSocAtFullSpeedChargeDateTime = ReachedMinimumSocAtFullSpeedChargeDateTime(car);

        //FullSpeed Aktivieren, wenn Minimum Soc nicht mehr erreicht werden kann
        if (reachedMinimumSocAtFullSpeedChargeDateTime > car.CarConfiguration.LatestTimeToReachSoC 
            && car.CarConfiguration.LatestTimeToReachSoC > DateTime.Now 
            || car.CarState.SoC < car.CarConfiguration.MinimumSoC && car.CarConfiguration.ChargeMode == ChargeMode.PvAndMinSoc)
        {
            car.CarState.AutoFullSpeedCharge = true;
        }
        //FullSpeed deaktivieren, wenn Minimum Soc erreicht wurde
        if (car.CarState.AutoFullSpeedCharge && car.CarState.SoC >= car.CarConfiguration.MinimumSoC)
        {
            car.CarState.AutoFullSpeedCharge = false;
        }

        //Falls MaxPower als Charge Mode: Leistung auf maximal
        if (car.CarConfiguration.ChargeMode == ChargeMode.MaxPower || car.CarState.AutoFullSpeedCharge)
        {
            _logger.LogDebug("Max Power Charging");
            if (teslaMateState.data.status.charging_details.charger_actual_current < maxAmpPerCar)
            {
                var ampToSet = maxAmpPerCar;

                if (teslaMateState.data.status.charging_details.charger_actual_current < 1)
                {
                    //Do not start charging when battery level near charge limit
                    if (teslaMateState.data.status.battery_details.battery_level >=
                        teslaMateState.data.status.charging_details.charge_limit_soc - 2)
                    {
                        return ampChange;
                    }
                    await StartCharging(teslaMateState.data.car.car_id, ampToSet, teslaMateState.data.status.state).ConfigureAwait(false);
                    ampChange += ampToSet - teslaMateState.data.status.charging_details.charger_actual_current;
                    UpdateEarliestTimesAfterSwitch(teslaMateState.data.car.car_id);
                }
                else
                {
                    await SetAmp(teslaMateState.data.car.car_id, ampToSet).ConfigureAwait(false);
                    ampChange += ampToSet - teslaMateState.data.status.charging_details.charger_actual_current;
                    UpdateEarliestTimesAfterSwitch(teslaMateState.data.car.car_id);
                }

            }

        }
        //Falls Laden beendet werden soll, aber noch ladend
        else if (finalAmpsToSet < minAmpPerCar && teslaMateState.data.status.charging_details.charger_actual_current > 0)
        {
            _logger.LogDebug("Charging should stop");
            var earliestSwitchOff = EarliestSwitchOff(teslaMateState.data.car.car_id);
            //Falls Klima an (Laden nicht deaktivierbar), oder Ausschaltbefehl erst seit Kurzem
            if (teslaMateState.data.status.climate_details.is_climate_on || earliestSwitchOff > DateTime.Now)
            {
                _logger.LogDebug("Can not stop charing: Climate on: {climateState}, earliest Switch Off: {earliestSwitchOff}",
                    teslaMateState.data.status.climate_details.is_climate_on,
                    earliestSwitchOff);
                if (teslaMateState.data.status.charging_details.charger_actual_current != minAmpPerCar)
                {
                    await SetAmp(teslaMateState.data.car.car_id, minAmpPerCar).ConfigureAwait(false);
                }
                ampChange += minAmpPerCar - teslaMateState.data.status.charging_details.charger_actual_current;
            }
            //Laden Stoppen
            else
            {
                _logger.LogDebug("Stop Charging");
                await StopCharging(teslaMateState.data.car.car_id).ConfigureAwait(false);
                ampChange -= teslaMateState.data.status.charging_details.charger_actual_current;
                UpdateEarliestTimesAfterSwitch(teslaMateState.data.car.car_id);
            }
        }
        //Falls Laden beendet ist und beendet bleiben soll
        else if (finalAmpsToSet < minAmpPerCar)
        {
            _logger.LogDebug("Charging should stay stopped");
            UpdateEarliestTimesAfterSwitch(teslaMateState.data.car.car_id);
        }
        //Falls nicht ladend, aber laden soll beginnen
        else if (finalAmpsToSet > minAmpPerCar && teslaMateState.data.status.charging_details.charger_actual_current == 0)
        {
            _logger.LogDebug("Charging should start");
            var earliestSwitchOn = EarliestSwitchOn(teslaMateState.data.car.car_id);

            if (earliestSwitchOn <= DateTime.Now)
            {
                _logger.LogDebug("Charging should start");
                var startAmp = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
                await StartCharging(teslaMateState.data.car.car_id, startAmp, teslaMateState.data.status.state).ConfigureAwait(false);
                ampChange += startAmp;
                UpdateEarliestTimesAfterSwitch(teslaMateState.data.car.car_id);
            }
        }
        //Normal Ampere setzen
        else
        {
            _logger.LogDebug("Normal amp set");
            UpdateEarliestTimesAfterSwitch(teslaMateState.data.car.car_id);
            var ampToSet = finalAmpsToSet > maxAmpPerCar ? maxAmpPerCar : finalAmpsToSet;
            if (ampToSet != teslaMateState.data.status.charging_details.charger_actual_current)
            {
                await SetAmp(teslaMateState.data.car.car_id, ampToSet).ConfigureAwait(false);
                ampChange += ampToSet - teslaMateState.data.status.charging_details.charger_actual_current;
            }
            else
            {
                _logger.LogDebug("Current actual amp: {currentActualAmp} same as amp to set: {ampToSet} Do not change anything",
                    teslaMateState.data.status.charging_details.charger_actual_current, ampToSet);
            }
        }

        return ampChange;
    }

    private static DateTime ReachedMinimumSocAtFullSpeedChargeDateTime(Car car)
    {
        var socToCharge = (double) car.CarConfiguration.MinimumSoC - car.CarState.SoC;
        if (socToCharge < 1)
        {
            return DateTime.Now + TimeSpan.Zero;
        }
        //ToDo: use correct soc/h with kWh
        return DateTime.Now + TimeSpan.FromHours(socToCharge / 15);
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

    private async Task StartCharging(int carId, int startAmp, string carState)
    {
        _logger.LogTrace("{method}({param1}, {param2}, {param3})", nameof(StartCharging), carId, startAmp, carState);

        if (carState.Equals("offline", StringComparison.CurrentCultureIgnoreCase) ||
            carState.Equals("asleep", StringComparison.CurrentCultureIgnoreCase))
        {
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

    private async Task<List<TeslaMateState>> GetTeslaMateStates(List<int> carIds)
    {
        _logger.LogTrace("{method}({@carIds})", nameof(GetTeslaMateStates), carIds);
        var teslaMateStates = new List<TeslaMateState>();

        foreach (var carId in carIds)
        {
            var stateUrl = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/status";
            using var httpClient = new HttpClient();
            var result = await httpClient.GetAsync(stateUrl).ConfigureAwait(false);
            try
            {
                result.EnsureSuccessStatusCode();
                var state = await result.Content.ReadFromJsonAsync<TeslaMateState>().ConfigureAwait(false);
                teslaMateStates.Add(state ?? throw new InvalidOperationException());
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Could not get state of car {carId}");
            }
        }
        return teslaMateStates;
    }
}