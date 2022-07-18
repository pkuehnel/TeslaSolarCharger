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
        private readonly IConfigurationWrapper _configurationWrapper;

        public BaseConfigurationController(IConfigurationWrapper configurationWrapper)
        {
            _configurationWrapper = configurationWrapper;
        }

        [HttpGet]
        public Task<DtoBaseConfiguration> GetBaseConfiguration() => _configurationWrapper.GetBaseConfigurationAsync();
    }
}
