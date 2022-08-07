using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace TeslaSolarCharger.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class HelloController : ControllerBase
    {
        [HttpGet]
        public Task<bool> IsAlive() => Task.FromResult(true);

        [HttpGet]
        public Task<string?> CurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            return Task.FromResult(fileVersionInfo.ProductVersion);
        }
    }
}
