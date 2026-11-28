using Microsoft.AspNetCore.Mvc;
using Plugins.SmaEnergymeter.Dtos;
using Plugins.SmaEnergymeter.Services;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace Plugins.SmaEnergymeter.Controllers
{
    public class CurrentPowerController : ApiBaseController
    {
        private readonly CurrentPowerService _currentPowerService;

        public CurrentPowerController(CurrentPowerService currentPowerService)
        {
            _currentPowerService = currentPowerService;
        }

        [HttpGet]
        public ActionResult<int> GetPower(uint? serialNumber = null)
        {
            var power = _currentPowerService.GetCurrentPower(serialNumber);
            return power.HasValue ? power.Value : NoCurrentValues();
        }

        [HttpGet]
        public ActionResult<DtoEnergyMeterValue> GetAllValues(uint? serialNumber = null)
        {
            var values = _currentPowerService.GetAllValues(serialNumber);
            return values != default ? values : NoCurrentValues();
        }

        /// <summary>
        /// Old values must not look valid: a consumer would otherwise keep using a frozen grid power.
        /// </summary>
        private ObjectResult NoCurrentValues()
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                $"No energy meter values received within the last {CurrentPowerService.MaxValueAge.TotalSeconds} seconds.");
        }
    }
}
