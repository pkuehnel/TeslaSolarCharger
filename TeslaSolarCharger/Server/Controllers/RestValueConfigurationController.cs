using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class RestValueConfigurationController(IRestValueConfigurationService service) : ApiBaseController
{
    [HttpGet]
    public async Task<ActionResult<List<DtoRestValueConfiguration>>> GetAllRestValueConfigurations()
    {
        var result = await service.GetAllRestValueConfigurations();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<int>> UpdateRestValueConfiguration([FromBody] DtoRestValueConfiguration dtoData)
    {
        return Ok(new DtoValue<int>(await service.SaveRestValueConfiguration(dtoData)));
    }
}
