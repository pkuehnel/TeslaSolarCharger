using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Contracts;

public interface IBaseConfigurationService
{
    Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration);
    Task UpdateMaxCombinedCurrent(int? maxCombinedCurrent);
    void UpdatePowerBuffer(int powerBuffer);
    Task<byte[]> DownloadBackup(string backupFileNameSuffix, string? backupZipDestinationDirectory);
    Task RestoreBackup(IFormFile file);
    Task<string> CreateLocalBackupZipFile(string backupFileNameSuffix, string? backupZipDestinationDirectory);
    Task<List<DtoRestConfigurationOverview>> GetRestValueOverviews();
}
