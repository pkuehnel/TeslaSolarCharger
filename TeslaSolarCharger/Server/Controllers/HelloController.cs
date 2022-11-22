using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class HelloController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly IChargingCostService _chargingCostService;

        public HelloController(ICoreService coreService, IChargingCostService chargingCostService)
        {
            _coreService = coreService;
            _chargingCostService = chargingCostService;
        }

        [HttpGet]
        public Task<bool> IsAlive() => Task.FromResult(true);

        [HttpGet]
        public Task<string?> ProductVersion()
        {
            return _coreService.GetCurrentVersion();
        }

        [HttpGet]
        public Task ChargingCost()
        {
            return _chargingCostService.AddPowerDistributionForAllChargingCars();
        }
    }
}
