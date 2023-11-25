using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class BaseConfigurationController : ApiBaseController
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

        [HttpGet]
        public void UpdateMaxCombinedCurrent(int? maxCombinedCurrent) =>
            _service.UpdateMaxCombinedCurrent(maxCombinedCurrent);

        [HttpGet]
        public void UpdatePowerBuffer(int powerBuffer) =>
            _service.UpdatePowerBuffer(powerBuffer);

        [HttpGet]
        public async Task<FileContentResult> DownloadBackup()
        {
            var bytes = await _service.DownloadBackup().ConfigureAwait(false);
            return File(bytes, "application/zip", "TSCBackup.zip");
        }
    }
}
