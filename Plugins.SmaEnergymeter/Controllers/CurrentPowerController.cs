using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Plugins.SmaEnergymeter.Services;

namespace Plugins.SmaEnergymeter.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CurrentPowerController : ControllerBase
    {
        private readonly CurrentPowerService _currentPowerService;

        public CurrentPowerController(CurrentPowerService currentPowerService)
        {
            _currentPowerService = currentPowerService;
        }

        [HttpGet]
        public int GetCurrentPower(int lastXSeconds)
        {
            return _currentPowerService.GetCurrentPower(lastXSeconds);
        }
    }
}
