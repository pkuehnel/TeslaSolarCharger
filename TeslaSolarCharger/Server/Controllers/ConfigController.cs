using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class ConfigController : ApiBaseController
    {
        private readonly IConfigJsonService _configJsonService;
        private readonly ITeslaFleetApiService _teslaFleetApiService;

        public ConfigController(IConfigJsonService configJsonService, ITeslaFleetApiService teslaFleetApiService)
        {
            _configJsonService = configJsonService;
            _teslaFleetApiService = teslaFleetApiService;
        }

        /// <summary>
        /// Get all settings and status of all cars
        /// </summary>
        [HttpGet]
        public ISettings GetSettings() => _configJsonService.GetSettings();

        /// <summary>
        /// Get basic Configuration of cars, which are not often changed
        /// </summary>
        [HttpGet]
        public Task<List<CarBasicConfiguration>> GetCarBasicConfigurations() => _configJsonService.GetCarBasicConfigurations();

        /// <summary>
        /// Update Car's configuration
        /// </summary>
        /// <param name="carId">Car Id of car to update</param>
        /// <param name="carBasicConfiguration">Car Configuration which should be set to car</param>
        [HttpPut]
        public Task UpdateCarBasicConfiguration(int carId, [FromBody] CarBasicConfiguration carBasicConfiguration) =>
            _configJsonService.UpdateCarBasicConfiguration(carId, carBasicConfiguration);
    }
}
