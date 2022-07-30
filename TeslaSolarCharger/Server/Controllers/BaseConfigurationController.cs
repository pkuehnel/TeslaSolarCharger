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
        private readonly IConfigurationWrapper _configurationWrapper;

        public BaseConfigurationController(IBaseConfigurationService service, IConfigurationWrapper configurationWrapper)
        {
            _service = service;
            _configurationWrapper = configurationWrapper;
        }

        [HttpGet]
        public Task<DtoBaseConfiguration> GetBaseConfiguration() => _configurationWrapper.GetBaseConfigurationAsync();

        [HttpPut]
        public void UpdateBaseConfiguration([FromBody] DtoBaseConfiguration baseConfiguration) =>
            _service.UpdateBaseConfigurationAsync(baseConfiguration);
    }
}
