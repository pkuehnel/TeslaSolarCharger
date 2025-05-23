using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class DebugController(IFleetTelemetryConfigurationService fleetTelemetryConfigurationService,
    IDebugService debugService,
    ITeslaFleetApiService teslaFleetApiService,
    IOcppChargePointConfigurationService ocppChargePointConfigurationService,
    ILoadPointManagementService loadPointManagementService) : ApiBaseController
{

    private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        Converters = new List<JsonConverter>
        {
            new StringEnumConverter(),
        },
    };

    [HttpGet]
    public IActionResult DownloadLogs()
    {
        var bytes = debugService.GetLogBytes();

        // Return the file with the appropriate content type and file name.
        return File(bytes, "text/plain", "logs.log");
    }

    [HttpGet]
    public IActionResult GetLogLevel()
    {
        var level = debugService.GetLogLevel();
        return Ok(new DtoValue<string>(level));
    }

    /// <summary>
    /// Adjusts the minimum log level for the in-memory sink.
    /// </summary>
    /// <param name="level">The new log level (e.g. Verbose, Debug, Information, Warning, Error, Fatal).</param>
    /// <returns>Status message.</returns>
    [HttpPost]
    public IActionResult SetLogLevel([FromQuery] string level)
    {
        debugService.SetLogLevel(level);
        return Ok();
    }

    [HttpGet]
    public IActionResult GetLogCapacity()
    {
        var capacity = debugService.GetLogCapacity();
        return Ok(new DtoValue<int>(capacity));
    }

    [HttpPost]
    public IActionResult SetLogCapacity([FromQuery] int capacity)
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
        var result = await debugService.StopCharging(chargepointId, connectorId,HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> SetCurrentAndPhases(string chargepointId, int connectorId, decimal currentToSet, int? numberOfPhases)
    {
        var result = await debugService.SetCurrentAndPhases(chargepointId, connectorId, currentToSet, numberOfPhases, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpGet]
    public async Task<IActionResult> GetChargePointConfigurationKeys(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.GetOcppConfigurations(chargepointId, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> SetMeterValuesSampledDataConfiguration(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.SetMeterValuesSampledDataConfiguration(chargepointId, HttpContext.RequestAborted);
        var resultString = JsonConvert.SerializeObject(result, _serializerSettings);
        return Ok(new DtoValue<string>(resultString));
    }

    [HttpPost]
    public async Task<IActionResult> SetMeterValuesSampleIntervalConfiguration(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.SetMeterValuesSampleIntervalConfiguration(chargepointId, HttpContext.RequestAborted);
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

    [HttpGet]
    public async Task<IActionResult> GetPluggedInLoadpoints()
    {
        var result = await loadPointManagementService.GetPluggedInLoadPoints();
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
    public async Task<IActionResult> GetEnergyLiveStatus(string energySiteId)
    {
        var products = await teslaFleetApiService.GetEnergyLiveStatus(energySiteId);
        return Ok(products.JsonResponse);
    }
}
