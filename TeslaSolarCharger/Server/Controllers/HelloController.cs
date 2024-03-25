using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class HelloController : ApiBaseController
    {
        private readonly ICoreService _coreService;
        private readonly ITscConfigurationService _tscConfigurationService;

        public HelloController(ICoreService coreService, ITscConfigurationService tscConfigurationService)
        {
            _coreService = coreService;
            _tscConfigurationService = tscConfigurationService;
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
        public Task<DtoValue<int>> HomeBatteryTargetChargingPower() => Task.FromResult(_coreService.HomeBatteryTargetChargingPower());
        
        [HttpGet]
        public async Task StopJobs() => await _coreService.StopJobs().ConfigureAwait(false);
        
        [HttpGet]
        public async Task DisconnectMqttServices() => await _coreService.DisconnectMqttServices().ConfigureAwait(false);

        [HttpGet]
        public Task<string?> ProductVersion()
        {
            return _coreService.GetCurrentVersion();
        }

        [HttpGet]
        public Task<DtoValue<int>> TeslaApiRequestsSinceStartup() => Task.FromResult(_coreService.TeslaApiRequestsSinceStartup());

        [HttpGet]
        public Task<DtoValue<bool>> ShouldDisplayApiRequestCounter() => Task.FromResult(_coreService.ShouldDisplayApiRequestCounter());

        [HttpGet]
        public Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to) => _coreService.GetPriceData(from, to);

        [HttpGet]
        public Task<string> GetInstallationId() => _coreService.GetInstallationId();
    }
}
