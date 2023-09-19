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
        public int GetPower(uint? serialNumber = null)
        {
            return _currentPowerService.GetCurrentPower(serialNumber);
        }

        [HttpGet]
        public DtoEnergyMeterValue GetAllValues(uint? serialNumber = null)
        {
            return _currentPowerService.GetAllValues(serialNumber);
        }
    }
}
