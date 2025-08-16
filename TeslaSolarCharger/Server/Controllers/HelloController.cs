﻿using Microsoft.AspNetCore.Mvc;
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
        public async Task<DtoValue<int>> NumberOfRelevantCars() => await (coreService.NumberOfRelevantLoadPoints());

        [HttpGet]
        public Task<DtoValue<int>> HomeBatteryTargetChargingPower() => Task.FromResult(coreService.HomeBatteryTargetChargingPower());

        [HttpGet]
        public async Task<IActionResult> ProductVersion()
        {
            var result = await coreService.GetCurrentVersion();
            return Ok(new DtoValue<string?>(result));
        }

        [HttpGet]
        public async Task<IActionResult> GetInstallationId()
        {
            var result = await coreService.GetInstallationId();
            return Ok(new DtoValue<string>(result));
        }

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
