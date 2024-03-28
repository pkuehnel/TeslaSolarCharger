using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class BaseConfigurationController(
        IBaseConfigurationService service,
        IConfigurationWrapper configurationWrapper)
        : ApiBaseController
    {
        [HttpGet]
        public Task<DtoBaseConfiguration> GetBaseConfiguration() => configurationWrapper.GetBaseConfigurationAsync();

        [HttpPut]
        public void UpdateBaseConfiguration([FromBody] DtoBaseConfiguration baseConfiguration) =>
            service.UpdateBaseConfigurationAsync(baseConfiguration);

        [HttpGet]
        public void UpdateMaxCombinedCurrent(int? maxCombinedCurrent) =>
            service.UpdateMaxCombinedCurrent(maxCombinedCurrent);

        [HttpGet]
        public void UpdatePowerBuffer(int powerBuffer) =>
            service.UpdatePowerBuffer(powerBuffer);

        [HttpGet]
        public Task<List<DtoRestConfigurationOverview>> GetRestValueConfigurations() =>
            service.GetRestValueOverviews();

        [HttpGet]
        public async Task<FileContentResult> DownloadBackup()
        {
            var bytes = await service.DownloadBackup(string.Empty, null).ConfigureAwait(false);
            return File(bytes, "application/zip", "TSCBackup.zip");
        }

        [HttpPost]
        public async Task RestoreBackup(IFormFile file)
        {
            await service.RestoreBackup(file).ConfigureAwait(false);
        }
    }
}
