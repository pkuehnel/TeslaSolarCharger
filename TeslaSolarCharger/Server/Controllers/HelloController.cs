using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class HelloController : ApiBaseController
    {
        private readonly ICoreService _coreService;

        public HelloController(ICoreService coreService)
        {
            _coreService = coreService;
        }

        [HttpGet]
        public Task<bool> IsAlive() => Task.FromResult(true);

        [HttpGet]
        public Task<DateTime> GetServerLocalTime() => Task.FromResult(_coreService.GetCurrentServerTime());

        [HttpGet]
        public Task<DtoValue<string>> GetServerTimeZoneDisplayName() => Task.FromResult(_coreService.GetServerTimeZoneDisplayName());

        [HttpGet]
        public Task<DtoValue<bool>> IsSolarEdgeInstallation() => Task.FromResult(_coreService.IsSolarEdgeInstallation());

        [HttpGet]
        public Task<DtoValue<int>> NumberOfRelevantCars() => Task.FromResult(_coreService.NumberOfRelevantCars());

        [HttpGet]
        public Task<DtoValue<int>> HomeBatteryTargetChargingPower() => Task.FromResult(_coreService.HomeBatteryTargetChargingPower());[HttpGet]
        
        [HttpGet]
        public async Task StopJobs() => await _coreService.StopJobs().ConfigureAwait(false);
        
        [HttpGet]
        public async Task DisconnectMqttServices() => await _coreService.DisconnectMqttServices().ConfigureAwait(false);

        [HttpGet]
        public Task<string?> ProductVersion()
        {
            return _coreService.GetCurrentVersion();
        }
    }
}
