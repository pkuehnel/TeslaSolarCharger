using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class ModbusValueConfigurationController(IModbusValueConfigurationService configurationService,
    IValueOverviewService valueOverviewService,
    IRefreshableValueHandlingService refreshableValueHandlingService) : ApiBaseController
{
    [HttpGet]
    public Task<List<DtoValueConfigurationOverview>> GetModbusValueOverviews() =>
        valueOverviewService.GetModbusValueOverviews();

    [HttpGet]
    public Task<DtoModbusConfiguration> GetValueConfigurationById(int id) =>
        configurationService.GetValueConfigurationById(id);

    [HttpGet]
    public Task<List<DtoModbusValueResultConfiguration>> GetResultConfigurationsByValueConfigurationId(int parentId) =>
        configurationService.GetResultConfigurationsByValueConfigurationId(parentId);

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> UpdateModbusValueConfiguration([FromBody] DtoModbusConfiguration dtoData)
    {
        var modbusConfigurationId = await configurationService.SaveModbusConfiguration(dtoData);
        await refreshableValueHandlingService.RecreateRefreshables(ConfigurationType.ModbusSolarValue, modbusConfigurationId).ConfigureAwait(false);
        return Ok(new DtoValue<int>(modbusConfigurationId));
    }
    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> SaveResultConfiguration(int parentId, [FromBody] DtoModbusValueResultConfiguration dtoData)
    {
        var modbusResultConfiugrationId = await configurationService.SaveModbusResultConfiguration(parentId, dtoData);
        await refreshableValueHandlingService.RecreateRefreshables(ConfigurationType.ModbusSolarValue, parentId).ConfigureAwait(false);
        return Ok(new DtoValue<int>(modbusResultConfiugrationId));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteModbusConfiguration(int id)
    {
        await configurationService.DeleteModbusConfiguration(id);
        await refreshableValueHandlingService.RecreateRefreshables(ConfigurationType.ModbusSolarValue, id).ConfigureAwait(false);
        return Ok();
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteResultConfiguration(int id)
    {
        await configurationService.DeleteResultConfiguration(id);
        //Do not limit deleting to an ID, simply recreate all to avoid need to lookup parent
        await refreshableValueHandlingService.RecreateRefreshables(ConfigurationType.ModbusSolarValue).ConfigureAwait(false);
        return Ok();
    }
}
