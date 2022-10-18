using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class HelloController : ControllerBase
    {
        private readonly ICoreService _coreService;

        public HelloController(ICoreService coreService)
        {
            _coreService = coreService;
        }

        [HttpGet]
        public Task<bool> IsAlive() => Task.FromResult(true);

        [HttpGet]
        public Task<string?> ProductVersion()
        {
            return _coreService.GetCurrentVersion();
        }
    }
}
