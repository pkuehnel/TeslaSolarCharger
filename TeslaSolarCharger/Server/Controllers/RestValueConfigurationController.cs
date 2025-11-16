using Microsoft.AspNetCore.Mvc;
using System.Configuration;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class RestValueConfigurationController(
    IRestValueConfigurationService service,
    IRestValueExecutionService executionService,
    IValueOverviewService overviewService,
    IGenericValueService genericValueHandlingService) : ApiBaseController
{
    [HttpGet]
    public async Task<ActionResult<List<DtoRestValueConfiguration>>> GetAllRestValueConfigurations()
    {
        var result = await service.GetAllRestValueConfigurations();
        return Ok(result);
    }

    [HttpGet]
    public Task<List<DtoValueConfigurationOverview>> GetRestValueConfigurations() =>
        overviewService.GetRestValueOverviews();

    [HttpPost]
    public async Task<ActionResult<DtoValue<string>>> DebugRestValueConfiguration([FromBody] DtoFullRestValueConfiguration config)
    {
        var result = await executionService.DebugRestValueConfiguration(config);
        return Ok(new DtoValue<string>(result));
    }

    [HttpGet]
    public async Task<ActionResult<DtoFullRestValueConfiguration>> GetFullRestValueConfigurationsById(int id)
    {
        var result = await service.GetFullRestValueConfigurationsByPredicate(c => c.Id == id);
        return Ok(result.Single());
    }

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> UpdateRestValueConfiguration([FromBody] DtoFullRestValueConfiguration dtoData)
    {
        var configurationId = await service.SaveRestValueConfiguration(dtoData).ConfigureAwait(false);
        await genericValueHandlingService.RecreateValues(ConfigurationType.RestSolarValue, configurationId);
        return Ok(new DtoValue<int>(configurationId));
    }

    [HttpGet]
    public async Task<ActionResult<List<DtoRestValueConfigurationHeader>>> GetHeadersByConfigurationId(int parentId)
    {
        var result = await service.GetHeadersByConfigurationId(parentId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> SaveHeader(int parentId, [FromBody] DtoRestValueConfigurationHeader dtoData)
    {
        var headerId = await service.SaveHeader(parentId, dtoData);
        await genericValueHandlingService.RecreateValues(ConfigurationType.RestSolarValue, parentId);
        return Ok(new DtoValue<int>(headerId));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteHeader(int id)
    {
        await service.DeleteHeader(id);
        await genericValueHandlingService.RecreateValues(ConfigurationType.RestSolarValue);
        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<List<DtoJsonXmlResultConfiguration>>> GetResultConfigurationsByConfigurationId(int parentId)
    {
        var result = await service.GetResultConfigurationsByConfigurationId(parentId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<DtoValue<int>>> SaveResultConfiguration(int parentId, [FromBody] DtoJsonXmlResultConfiguration dtoData)
    {
        var resultConfigurationId = await service.SaveResultConfiguration(parentId, dtoData);
        await genericValueHandlingService.RecreateValues(ConfigurationType.RestSolarValue, parentId);
        return Ok(new DtoValue<int>(resultConfigurationId));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteResultConfiguration(int id)
    {
        await service.DeleteResultConfiguration(id);
        await genericValueHandlingService.RecreateValues(ConfigurationType.RestSolarValue);
        return Ok();
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteRestValueConfiguration(int id)
    {
        await service.DeleteRestValueConfiguration(id);
        await genericValueHandlingService.RecreateValues(ConfigurationType.RestSolarValue, id);
        return Ok();
    }
}
