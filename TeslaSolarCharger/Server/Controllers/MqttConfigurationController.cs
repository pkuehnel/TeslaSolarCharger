using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class MqttConfigurationController(IMqttConfigurationService configurationService) : ApiBaseController
{
    [HttpGet]
    public Task<DtoMqttConfiguration> GetConfigurationById(int id) =>
        configurationService.GetConfigurationById(id);

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> SaveConfiguration([FromBody] DtoMqttConfiguration dtoData)
    {
        return Ok(new DtoValue<int>(await configurationService.SaveConfiguration(dtoData)));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteConfiguration(int id)
    {
        await configurationService.DeleteConfiguration(id);
        return Ok();
    }

    [HttpGet]
    public Task<DtoMqttResultConfiguration> GetResultConfigurationById(int id) =>
        configurationService.GetResultConfigurationById(id);

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> SaveResultConfiguration(int parentId, [FromBody] DtoMqttResultConfiguration dtoData)
    {
        return Ok(new DtoValue<int>(await configurationService.SaveResultConfiguration(parentId, dtoData)));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteResultConfiguration(int id)
    {
        await configurationService.DeleteResultConfiguration(id);
        return Ok();
    }
}
