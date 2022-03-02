using Microsoft.AspNetCore.Mvc;
using SmartTeslaAmpSetter.Server.Services;
using SmartTeslaAmpSetter.Shared.Dtos;
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
        public Settings GetSettings() => _service.GetSettings();

        /// <summary>
        /// Change Chargemode of car
        /// </summary>
        /// <param name="carId">Car id whose chargemode should be changed</param>
        /// <returns>Resulting chargemode after change</returns>
        [HttpPost]
        public ChargeMode ChangeChargeMode([FromBody] int carId) => _service.ChangeChargeMode(carId);

        /// <summary>
        /// Update Car's configuration or status. Note: Car Status is periodically overwritten by TeslaMate
        /// </summary>
        /// <param name="car">Car with new property values</param>
        [HttpPut]
        public void UpdateCar([FromBody] Car car) => _service.UpdateCar(car);
    }
}
