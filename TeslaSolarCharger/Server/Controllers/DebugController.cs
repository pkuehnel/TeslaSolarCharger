using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Controllers;

public class DebugController(
    IFleetTelemetryConfigurationService fleetTelemetryConfigurationService,
    IDebugService debugService,
    ITeslaFleetApiService teslaFleetApiService,
    IOcppChargePointConfigurationService ocppChargePointConfigurationService,
    ITscOnlyChargingCostService tscOnlyChargingCostService,
    IHomeBatteryModeService homeBatteryModeService,
    IBleService bleService,
    ISettings settings) : ApiBaseController
{

    private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented, Converters = new List<JsonConverter> { new StringEnumConverter(), },
    };

    [HttpGet]
    public async Task<IActionResult> GetHomeBatteryControlState()
    {
        var state = await homeBatteryModeService.GetControlStateAsync(HttpContext.RequestAborted);
        return Ok(state);
    }

    [HttpPost]
    public async Task<IActionResult> SetHomeBatteryMode(HomeBatteryMode mode, int durationMinutes)
    {
        await homeBatteryModeService.SetManualModeAsync(mode, TimeSpan.FromMinutes(durationMinutes), HttpContext.RequestAborted);
        var state = await homeBatteryModeService.GetControlStateAsync(HttpContext.RequestAborted);
        return Ok(state);
    }

    [HttpPost]
    public async Task<IActionResult> ClearHomeBatteryMode()
    {
        await homeBatteryModeService.ClearManualModeAsync(HttpContext.RequestAborted);
        var state = await homeBatteryModeService.GetControlStateAsync(HttpContext.RequestAborted);
        return Ok(state);
    }

    /// <summary>
    /// Gets the current startup logs
    /// </summary>
    /// <returns>List of log entries</returns>
    [HttpGet]
    public ActionResult<List<string>> GetLogs(int tail)
    {
        var logs = debugService.GetLogs(tail);
        return Ok(logs);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadInMemoryLogs()
    {
        var stream = new MemoryStream();
        await debugService.StreamLogsToAsync(stream);
        stream.Position = 0; // Reset position to beginning

        return File(stream, "text/plain", "logs.log");
    }

    [HttpGet]
    public async Task DownloadFileLogs()
    {
        // Enable synchronous I/O for this request only
        var syncIoFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (syncIoFeature != null)
        {
            syncIoFeature.AllowSynchronousIO = true;
        }

        Response.ContentType = "application/zip";
        Response.Headers.Add("Content-Disposition", "attachment; filename=\"logs.zip\"");

        await debugService.WriteFileLogsToStream(Response.Body);
    }

    [HttpGet]
    public IActionResult GetInMemoryLogLevel()
    {
        var level = debugService.GetInMemoryLogLevel();
        return Ok(new DtoValue<string>(level));
    }

    [HttpGet]
    public IActionResult GetFileLogLevel()
    {
        var level = debugService.GetFileLogLevel();
        return Ok(new DtoValue<string>(level));
    }

    /// <summary>
    /// Adjusts the minimum log level for the in-memory sink.
    /// </summary>
    /// <param name="level">The new log level (e.g. Verbose, Debug, Information, Warning, Error, Fatal).</param>
    /// <returns>Status message.</returns>
    [HttpPost]
    public IActionResult SetInMemoryLogLevel([FromQuery] string level)
    {
        debugService.SetInMemoryLogLevel(level);
        return Ok();
    }

    [HttpPost]
    public IActionResult SetFileLogLevel([FromQuery] string level)
    {
        debugService.SetFileLogLevel(level);
        return Ok();
    }

    [HttpGet]
    public IActionResult GetInMemoryLogCapacity()
    {
        var capacity = debugService.GetLogCapacity();
        return Ok(new DtoValue<int>(capacity));
    }

    [HttpPost]
    public IActionResult SetInMemoryLogCapacity([FromQuery] int capacity)
    {
        debugService.SetLogCapacity(capacity);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetFleetTelemetryConfiguration(string vin)
    {
        var config = await fleetTelemetryConfigurationService.GetFleetTelemetryConfiguration(vin);
        var configString = JsonConvert.SerializeObject(config, _serializerSettings);
        return Ok(new DtoValue<string>(configString));
    }

    [HttpPost]
    public async Task<IActionResult> SetFleetTelemetryConfiguration(string vin, bool forceReconfiguration)
    {
        var config = await fleetTelemetryConfigurationService.SetFleetTelemetryConfiguration(vin, forceReconfiguration);
        var configString = JsonConvert.SerializeObject(config, _serializerSettings);
        return Ok(new DtoValue<string>(configString));
    }

    [HttpGet]
    public async Task<IActionResult> GetCars()
    {
        var cars = await debugService.GetCars();
        return Ok(cars);
    }

    [HttpGet]
    public async Task<IActionResult> GetChargingConnectors()
    {
        var connectors = await debugService.GetChargingConnectors();
        return Ok(connectors);
    }

    [HttpPost]
    public async Task<IActionResult> StartCharging(string chargepointId, int connectorId, decimal currentToSet, int? numberOfPhases)
    {
        var result = await debugService.StartCharging(chargepointId, connectorId, currentToSet, numberOfPhases, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> StopCharging(string chargepointId, int connectorId)
    {
        var result = await debugService.StopCharging(chargepointId, connectorId, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> SetCurrentAndPhases(string chargepointId, int connectorId, decimal currentToSet, int? numberOfPhases)
    {
        var result = await debugService.SetCurrentAndPhases(chargepointId, connectorId, currentToSet, numberOfPhases,
            HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpGet]
    public async Task<IActionResult> GetChargePointConfigurationKey(string chargepointId, string configurationKey)
    {
        var result = await ocppChargePointConfigurationService.GetOcppConfigurations(chargepointId, configurationKey, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> SetMeterValuesSampledDataConfiguration(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.SetMeterValuesSampledDataConfiguration(chargepointId,
            HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> SetMeterValuesSampleIntervalConfiguration(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.SetMeterValuesSampleIntervalConfiguration(chargepointId,
            HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> RebootCharger(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.RebootCharger(chargepointId, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> TriggerStatusNotification(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.TriggerStatusNotification(chargepointId, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> TriggerMeterValues(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.TriggerMeterValues(chargepointId, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpGet]
    public IActionResult GetOcppConnectorState(int connectorId)
    {
        var result = debugService.GetOcppConnectorState(connectorId);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpGet]
    public IActionResult GetDtoCar(int carId)
    {
        var result = debugService.GetDtoCar(carId);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> WakeUpCar(int carId)
    {
        var result = await teslaFleetApiService.WakeUpCar(carId, true);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> SetChargingAmps(int carId, int amps)
    {
        var result = await teslaFleetApiService.SetChargingAmps(carId, amps);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> GetVehicleOnlineState(int carId)
    {
        var result = await teslaFleetApiService.GetVehicleOnlineState(carId);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> GetBleBodyControllerState(int carId)
    {
        var vin = settings.Cars.FirstOrDefault(c => c.Id == carId)?.Vin;
        if (string.IsNullOrEmpty(vin))
        {
            return BadRequest("Car has no VIN");
        }
        var result = await bleService.GetBodyControllerState(vin);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> GetBleChargeState(int carId)
    {
        var vin = settings.Cars.FirstOrDefault(c => c.Id == carId)?.Vin;
        if (string.IsNullOrEmpty(vin))
        {
            return BadRequest("Car has no VIN");
        }
        var result = await bleService.GetChargeState(vin);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    /// <summary>
    /// Passive beacon scan for one car: it never connects, so it can not wake the car. Next to the presence answer the
    /// result carries the advertisement counters of the scan window, which tell an absent car apart from a radio that
    /// hears nothing at all.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GetBleBeaconScanResult(int carId)
    {
        var vin = settings.Cars.FirstOrDefault(c => c.Id == carId)?.Vin;
        if (string.IsNullOrEmpty(vin))
        {
            return BadRequest("Car has no VIN");
        }
        var result = await bleService.GetBeaconScanResultForVin(vin);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpGet]
    public async Task<IActionResult> GetAllProductsFromTeslaAccount()
    {
        var products = await teslaFleetApiService.GetAllProductsFromTeslaAccount();
        return Ok(products.JsonResponse);
    }

    [HttpGet]
    public async Task<IActionResult> GetEnergySites()
    {
        var sites = await teslaFleetApiService.GetEnergySites();
        return Ok(sites);
    }

    [HttpGet]
    public async Task<IActionResult> GetEnergyLiveStatus(string energySiteId)
    {
        var products = await teslaFleetApiService.GetEnergyLiveStatus(energySiteId);
        return Ok(products.JsonResponse);
    }

    [HttpGet]
    public async Task<IActionResult> GetLastestMeterValues()
    {
        var result = await debugService.GetLatestMeterValues();
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpGet]
    public async Task<IActionResult> GetGridPrices(DateTimeOffset from, DateTimeOffset to)
    {
        var result = await tscOnlyChargingCostService.GetPricesInTimeSpan(from, to);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> ClearTeslaTokenEncryptionKeys()
    {
        var result = await debugService.ClearTeslaTokenEncryptionKeys();
        return Ok(new DtoValue<int>(result));
    }
}
