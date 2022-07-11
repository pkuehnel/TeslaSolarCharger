using Microsoft.AspNetCore.Mvc;

namespace TeslaSolarCharger.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class HelloController : ControllerBase
    {
        [HttpGet]
        public Task<bool> IsAlive() => Task.FromResult(true);
    }
}
