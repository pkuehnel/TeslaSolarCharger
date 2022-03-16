using Microsoft.AspNetCore.Mvc;
using SmartTeslaAmpSetter.Server.Services;
using SmartTeslaAmpSetter.Shared.Dtos;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.Enums;

namespace SmartTeslaAmpSetter.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly ConfigService _service;

        public ConfigController(ConfigService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get all settings and status of all cars
        /// </summary>
        [HttpGet]
        public Task<Settings> GetSettings() => _service.GetSettings();

        /// <summary>
        /// Change Chargemode of car
        /// </summary>
        /// <param name="carId">Car id whose chargemode should be changed</param>
        /// <returns>Resulting chargemode after change</returns>
        [HttpPost]
        public ChargeMode ChangeChargeMode(int carId) => _service.ChangeChargeMode(carId);

        /// <summary>
        /// Update Car's configuration
        /// </summary>
        /// <param name="carId">Car Id of car to update</param>
        /// <param name="carConfiguration">Car Configuration which should be set to car</param>
        [HttpPut]
        public void UpdateCarConfiguration(int carId, [FromBody] CarConfiguration carConfiguration) =>
            _service.UpdateCarConfiguration(carId, carConfiguration);

        /// <summary>
        /// Get basic Configuration of cars, which are not often changed
        /// </summary>
        [HttpGet]
        public List<CarBasicConfiguration> GetCarBasicConfigurations() => _service.GetCarBasicConfigurations();

        /// <summary>
        /// Update Car's configuration
        /// </summary>
        /// <param name="carId">Car Id of car to update</param>
        /// <param name="carConfiguration">Car Configuration which should be set to car</param>
        [HttpPut]
        public void UpdateCarBasicConfiguration(int carId, [FromBody] CarBasicConfiguration carBasicConfiguration) =>
            _service.UpdateCarBasicConfiguration(carId, carBasicConfiguration);
    }
}
