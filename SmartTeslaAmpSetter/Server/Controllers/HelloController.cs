using Microsoft.AspNetCore.Mvc;

namespace SmartTeslaAmpSetter.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class HelloController : ControllerBase
    {
        [HttpGet]
        public Task<bool> IsAlive() => Task.FromResult(true);
    }
}
