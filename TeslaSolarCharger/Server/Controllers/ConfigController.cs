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
        IFleetTelemetryConfigurationService fleetTelemetryConfigurationService,
        ICarConfigurationService carConfigurationService)
        : ApiBaseController
    {

        /// <summary>
        /// Discover and add cars that are present in the connected Tesla account but not yet in TSC.
        /// Mirrors the discovery that runs at startup, so users can pick up newly added Teslas without restarting.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RefreshTeslaCarsFromAccount()
        {
            await carConfigurationService.AddAllMissingCarsFromTeslaAccount().ConfigureAwait(false);
            await configJsonService.AddCarsToSettings(null).ConfigureAwait(false);
            return Ok();
        }

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

        [HttpPost]
        public Task DisconnectCarFromSmartCar(int carId)
        {
            return configJsonService.DisconnectCarFromSmartCar(carId);
        }
    }
}
