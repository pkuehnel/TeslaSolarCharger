using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;
using TeslaSolarCharger.SharedBackend.Extensions;

namespace TeslaSolarCharger.Server.Controllers
{
    public class HelloController(
        ICoreService coreService)
        : ApiBaseController
    {

        [HttpGet]
        public Task<bool> IsAlive() => Task.FromResult(true);

        [HttpGet]
        public Task<DateTime> GetServerLocalTime() => Task.FromResult(coreService.GetCurrentServerTime());

        [HttpGet]
        public Task<DtoValue<string>> GetServerTimeZoneDisplayName() => Task.FromResult(coreService.GetServerTimeZoneDisplayName());

        [HttpGet]
        public Task<DtoValue<bool>> IsSolarEdgeInstallation() => Task.FromResult(coreService.IsSolarEdgeInstallation());

        [HttpGet]
        public Task<DtoValue<int>> NumberOfRelevantCars() => Task.FromResult(coreService.NumberOfRelevantCars());

        [HttpGet]
        public Task<DtoValue<int>> HomeBatteryTargetChargingPower() => Task.FromResult(coreService.HomeBatteryTargetChargingPower());
        
        [HttpGet]
        public async Task StopJobs() => await coreService.StopJobs().ConfigureAwait(false);
        
        [HttpGet]
        public async Task DisconnectMqttServices() => await coreService.DisconnectMqttServices().ConfigureAwait(false);

        [HttpGet]
        public Task<string?> ProductVersion()
        {
            return coreService.GetCurrentVersion();
        }

        [HttpGet]
        public Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to) => coreService.GetPriceData(from, to);

        [HttpGet]
        public Task<string> GetInstallationId() => coreService.GetInstallationId();

        [HttpGet]
        public Dictionary<int, string> GetRawRestRequestResults() => coreService.GetRawRestRequestResults();

        [HttpGet]
        public Dictionary<int, string> GetRawRestValue() => coreService.GetRawRestValue();

        [HttpGet]
        public Dictionary<int, decimal?> GetCalculatedRestValue() => coreService.GetCalculatedRestValue();

        [HttpGet]
        public DtoValue<bool> IsStartupCompleted() => new(coreService.IsStartupCompleted());

        [HttpGet]
        public async Task<IActionResult> SendTestTelegramMessage()
        {
            var result = await coreService.SendTestTelegramMessage().ConfigureAwait(false);
            return result.ToOk();
            
        }
    }
}
