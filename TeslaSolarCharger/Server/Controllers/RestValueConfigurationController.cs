using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class RestValueConfigurationController(IRestValueConfigurationService service,
    IRestValueExecutionService executionService) : ApiBaseController
{
    [HttpGet]
    public async Task<ActionResult<List<DtoRestValueConfiguration>>> GetAllRestValueConfigurations()
    {
        var result = await service.GetAllRestValueConfigurations();
        return Ok(result);
    }

    [HttpGet]
    public Task<List<DtoValueConfigurationOverview>> GetRestValueConfigurations() =>
        executionService.GetRestValueOverviews();

    [HttpGet]
    public async Task<ActionResult<DtoFullRestValueConfiguration>> GetFullRestValueConfigurationsById(int id)
    {
        var result = await service.GetFullRestValueConfigurationsByPredicate(c => c.Id == id);
        return Ok(result.Single());
    }

    [HttpPost]
    public async Task<ActionResult<int>> UpdateRestValueConfiguration([FromBody] DtoFullRestValueConfiguration dtoData)
    {
        return Ok(new DtoValue<int>(await service.SaveRestValueConfiguration(dtoData)));
    }

    [HttpGet]
    public async Task<ActionResult<List<DtoRestValueConfigurationHeader>>> GetHeadersByConfigurationId(int parentId)
    {
        var result = await service.GetHeadersByConfigurationId(parentId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<int>> SaveHeader(int parentId, [FromBody] DtoRestValueConfigurationHeader dtoData)
    {
        return Ok(new DtoValue<int>(await service.SaveHeader(parentId, dtoData)));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteHeader(int id)
    {
        await service.DeleteHeader(id);
        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<List<DtoRestValueResultConfiguration>>> GetResultConfigurationsByConfigurationId(int parentId)
    {
        var result = await service.GetResultConfigurationsByConfigurationId(parentId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<int>> SaveResultConfiguration(int parentId, [FromBody] DtoRestValueResultConfiguration dtoData)
    {
        return Ok(new DtoValue<int>(await service.SaveResultConfiguration(parentId, dtoData)));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteResultConfiguration(int id)
    {
        await service.DeleteResultConfiguration(id);
        return Ok();
    }
}
