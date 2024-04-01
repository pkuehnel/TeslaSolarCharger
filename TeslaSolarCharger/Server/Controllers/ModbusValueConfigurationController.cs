using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class ModbusValueConfigurationController(IModbusValueConfigurationService configurationService,
    IModbusValueExecutionService executionService) : ApiBaseController
{
    [HttpGet]
    public Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews() =>
        executionService.GetModbusValueOverviews();

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> UpdateRestValueConfiguration([FromBody] DtoModbusConfiguration dtoData)
    {
        return Ok(new DtoValue<int>(await configurationService.SaveModbusConfiguration(dtoData)));
    }
}
