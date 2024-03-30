using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class RestValueExecutionController(IRestValueExecutionService service) : ApiBaseController
{
    [HttpPost]
    public async Task<ActionResult<DtoValue<string>>> DebugRestValueConfiguration([FromBody] DtoFullRestValueConfiguration config)
    {
        var result = await service.DebugRestValueConfiguration(config);
        return Ok(new DtoValue<string>(result));
    }
}
