using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Contracts;

public interface IBaseConfigurationService
{
    Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration);
    Task UpdateMaxCombinedCurrent(int? maxCombinedCurrent);
    Task UpdatePowerBuffer(int powerBuffer);
    Task RestoreBackup(IFormFile file);
    Task<string> CreateLocalBackupZipFile(string backupFileNamePrefix, string? backupZipDestinationDirectory, bool clearBackupDirectoryBeforeBackup);
    List<DtoBackupFileInformation> GetAutoBackupFileInformations();
    Task<byte[]> DownloadAutoBackup(string fileName);
    Task<(Stream stream, string fileName)> DownloadBackupStream(string? backupZipDestinationDirectory);
}
