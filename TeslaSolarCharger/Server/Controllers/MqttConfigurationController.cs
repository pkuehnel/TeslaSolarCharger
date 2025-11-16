using Microsoft.AspNetCore.Mvc;
using System.Configuration;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class MqttConfigurationController(IMqttConfigurationService configurationService, IValueOverviewService valueOverviewService,
    IGenericValueService genericValueHandlingService) : ApiBaseController
{
    [HttpGet]
    public Task<List<DtoValueConfigurationOverview>> GetMqttValueOverviews() =>
        valueOverviewService.GetMqttValueOverviews();

    [HttpGet]
    public Task<DtoMqttConfiguration> GetConfigurationById(int id) =>
        configurationService.GetConfigurationById(id);

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> SaveConfiguration([FromBody] DtoMqttConfiguration dtoData)
    {
        var configurationId = await configurationService.SaveConfiguration(dtoData);
        await genericValueHandlingService.RecreateValues(ConfigurationType.MqttSolarValue, configurationId);
        return Ok(new DtoValue<int>(configurationId));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteConfiguration(int id)
    {
        await configurationService.DeleteConfiguration(id);
        await genericValueHandlingService.RecreateValues(ConfigurationType.MqttSolarValue, id);
        return Ok();
    }

    [HttpGet]
    public Task<List<DtoMqttResultConfiguration>> GetResultConfigurationsByParentId(int parentId) =>
        configurationService.GetResultConfigurationsByParentId(parentId);

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> SaveResultConfiguration(int parentId, [FromBody] DtoMqttResultConfiguration dtoData)
    {
        var resultConfigurationId = await configurationService.SaveResultConfiguration(parentId, dtoData);
        await genericValueHandlingService.RecreateValues(ConfigurationType.MqttSolarValue, parentId);
        return Ok(new DtoValue<int>(resultConfigurationId));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteResultConfiguration(int id)
    {
        await configurationService.DeleteResultConfiguration(id);
        await genericValueHandlingService.RecreateValues(ConfigurationType.MqttSolarValue);
        return Ok();
    }
}
