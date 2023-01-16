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

        //ToDo: needs to be changed to DtoValue<DateTime>
        [HttpGet]
        public Task<DtoValue<DateTime>> GetServerLocalTime() => Task.FromResult(_coreService.GetCurrentServerTime());

        [HttpGet]
        public Task<DtoValue<bool>> IsSolarEdgeInstallation() => Task.FromResult(_coreService.IsSolarEdgeInstallation());

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
