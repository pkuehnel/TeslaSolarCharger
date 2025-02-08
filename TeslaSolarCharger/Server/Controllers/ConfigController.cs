using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class ConfigController(IConfigJsonService configJsonService,
        IFleetTelemetryConfigurationService fleetTelemetryConfigurationService)
        : ApiBaseController
    {

        /// <summary>
        /// Get all settings and status of all cars
        /// </summary>
        [HttpGet]
        public ISettings GetSettings() => configJsonService.GetSettings();

        /// <summary>
        /// Get basic Configuration of cars, which are not often changed
        /// </summary>
        [HttpGet]
        public Task<List<CarBasicConfiguration>> GetCarBasicConfigurations() => configJsonService.GetCarBasicConfigurations();

        /// <summary>
        /// Update Car's configuration
        /// </summary>
        /// <param name="carId">Car Id of car to update</param>
        /// <param name="carBasicConfiguration">Car Configuration which should be set to car</param>
        [HttpPost]
        public Task UpdateCarBasicConfiguration(int carId, [FromBody] CarBasicConfiguration carBasicConfiguration)
        {
            return configJsonService.UpdateCarBasicConfiguration(carId, carBasicConfiguration);
        }

        [HttpGet]
        public async Task<IActionResult> GetFleetTelemetryConfiguration(string vin)
        {
            var config = await fleetTelemetryConfigurationService.GetFleetTelemetryConfiguration(vin);
            var configString = JsonConvert.SerializeObject(config, Formatting.Indented);
            return Ok(new DtoValue<string>(configString));
        }
    }
}
