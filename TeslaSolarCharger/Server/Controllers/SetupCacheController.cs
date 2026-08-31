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
        var setupCache = await setupCacheService.GetSetupCache();
        if (setupCache == null)
        {
            // Return 204 instead of a 200 with a null body: the client's snackbar-wrapped GET reports a
            // deserialized-null payload as an error, which would show a spurious error toast on a fresh setup
            // (where there is no cache yet). 204 is mapped to "no value" by the client without an error.
            return NoContent();
        }
        return setupCache;
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
