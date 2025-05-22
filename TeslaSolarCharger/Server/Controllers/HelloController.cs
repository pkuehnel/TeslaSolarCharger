using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
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
        public IActionResult GetServerLocalTime()
        {
            var result = coreService.GetCurrentServerTime();
            return Ok(new DtoValue<DateTime>(result));
        }

        [HttpGet]
        public IActionResult GetServerTimeZoneDisplayName()
        {
            var result = coreService.GetServerTimeZoneDisplayName();
            return Ok(result);
        }

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
        public async Task<IActionResult> ProductVersion()
        {
            var result = await coreService.GetCurrentVersion();
            return Ok(new DtoValue<string?>(result));
        }

        [HttpGet]
        public Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to) => coreService.GetPriceData(from, to);

        [HttpGet]
        public async Task<IActionResult> GetInstallationId()
        {
            var result = await coreService.GetInstallationId();
            return Ok(new DtoValue<string>(result));
        }

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
