using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetupCacheController(ISetupCacheService setupCacheService) : ControllerBase
{
    [HttpGet("GetSetupCache")]
    public async Task<ActionResult<DtoSetupCache?>> GetSetupCache()
    {
        return await setupCacheService.GetSetupCache();
    }

    [HttpPost("UpdateSetupCache")]
    public async Task<ActionResult> UpdateSetupCache([FromBody] DtoSetupCache setupCache)
    {
        await setupCacheService.UpdateSetupCache(setupCache);
        return Ok();
    }

    [HttpDelete("DeleteSetupCache")]
    public async Task<ActionResult> DeleteSetupCache()
    {
        await setupCacheService.DeleteSetupCache();
        return Ok();
    }
}
