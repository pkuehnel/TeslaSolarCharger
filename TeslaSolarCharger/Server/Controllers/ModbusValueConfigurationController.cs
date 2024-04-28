using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class ModbusValueConfigurationController(IModbusValueConfigurationService configurationService,
    IModbusValueExecutionService executionService) : ApiBaseController
{
    [HttpGet]
    public Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews() =>
        executionService.GetModbusValueOverviews();

    [HttpGet]
    public Task<DtoModbusConfiguration> GetValueConfigurationById(int id) =>
        configurationService.GetValueConfigurationById(id);

    [HttpGet]
    public Task<List<DtoModbusValueResultConfiguration>> GetResultConfigurationsByValueConfigurationId(int parentId) =>
        configurationService.GetResultConfigurationsByValueConfigurationId(parentId);

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> UpdateModbusValueConfiguration([FromBody] DtoModbusConfiguration dtoData)
    {
        return Ok(new DtoValue<int>(await configurationService.SaveModbusConfiguration(dtoData)));
    }
    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> SaveResultConfiguration(int parentId, [FromBody] DtoModbusValueResultConfiguration dtoData)
    {
        return Ok(new DtoValue<int>(await configurationService.SaveModbusResultConfiguration(parentId, dtoData)));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteModbusConfiguration(int id)
    {
        await configurationService.DeleteModbusConfiguration(id);
        return Ok();
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteResultConfiguration(int id)
    {
        await configurationService.DeleteResultConfiguration(id);
        return Ok();
    }
}
