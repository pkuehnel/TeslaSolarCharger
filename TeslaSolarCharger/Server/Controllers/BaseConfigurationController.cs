using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
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

        [HttpGet]
        public DtoValue<bool> AllowPowerBufferChangeOnHome() => new(configurationWrapper.AllowPowerBufferChangeOnHome());

        [HttpGet]
        public DtoValue<bool> IsPredictSolarPowerGenerationEnabled() => new(configurationWrapper.IsPredictSolarPowerGenerationEnabled());

        [HttpGet]
        public DtoValue<bool> ShowEnergyDataOnHome() => new(configurationWrapper.ShowEnergyDataOnHome());

        [HttpPost]
        public Task UpdateBaseConfiguration([FromBody] DtoBaseConfiguration baseConfiguration) =>
            service.UpdateBaseConfigurationAsync(baseConfiguration);

        [HttpGet]
        public Task UpdateMaxCombinedCurrent(int? maxCombinedCurrent) =>
            service.UpdateMaxCombinedCurrent(maxCombinedCurrent);

        [HttpGet]
        public DtoValue<int> PowerBuffer() => new(configurationWrapper.PowerBuffer());

        [HttpGet]
        public Task UpdatePowerBuffer(int powerBuffer) =>
            service.UpdatePowerBuffer(powerBuffer);

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

        [HttpGet]
        public List<DtoBackupFileInformation> GetAutoBackupFileInformations() => service.GetAutoBackupFileInformations();

        [HttpGet]
        public async Task<FileContentResult> DownloadAutoBackup(string fileName)
        {
            var bytes = await service.DownloadAutoBackup(fileName).ConfigureAwait(false);
            return File(bytes, "application/zip", fileName);
        }
    }
}
