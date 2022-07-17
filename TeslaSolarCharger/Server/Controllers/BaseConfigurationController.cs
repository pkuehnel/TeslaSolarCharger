using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class BaseConfigurationController : ControllerBase
    {
        private readonly IBaseConfigurationService _service;

        public BaseConfigurationController(IBaseConfigurationService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<DtoBaseConfiguration> GetBaseConfiguration() => _service.GetBaseConfiguration();
    }
}
