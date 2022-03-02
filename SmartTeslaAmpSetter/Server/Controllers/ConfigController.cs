using Microsoft.AspNetCore.Mvc;
using SmartTeslaAmpSetter.Server.Services;
using SmartTeslaAmpSetter.Shared.Dtos;

namespace SmartTeslaAmpSetter.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly ConfigService _service;

        public ConfigController(ConfigService service)
        {
            _service = service;
        }

        [HttpGet] 
        public Settings GetSettings() => _service.GetSettings();
    }
}
