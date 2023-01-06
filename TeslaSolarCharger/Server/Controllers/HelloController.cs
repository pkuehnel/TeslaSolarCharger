using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class HelloController : ControllerBase
    {
        private readonly ICoreService _coreService;

        public HelloController(ICoreService coreService)
        {
            _coreService = coreService;
        }

        [HttpGet]
        public Task<bool> IsAlive() => Task.FromResult(true);

        [HttpGet]
        public Task<DtoValue<int>> NumberOfRelevantCars() => Task.FromResult(_coreService.NumberOfRelevantCars());

        [HttpGet]
        public Task<DtoValue<int>> HomeBatteryTargetChargingPower() => Task.FromResult(_coreService.HomeBatteryTargetChargingPower());

        [HttpGet]
        public Task<string?> ProductVersion()
        {
            return _coreService.GetCurrentVersion();
        }
    }
}
