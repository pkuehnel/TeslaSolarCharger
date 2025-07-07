using Microsoft.Data.Sqlite;
using System.IO.Compression;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BaseConfigurationService(
    ILogger<BaseConfigurationService> logger,
    IConfigurationWrapper configurationWrapper,
    JobManager jobManager,
    ITeslaMateMqttService teslaMateMqttService,
    ISettings settings,
    IDbConnectionStringHelper dbConnectionStringHelper,
    IConstants constants)
    : IBaseConfigurationService
{
    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration)
    {
        logger.LogTrace("{method}({@baseConfiguration})", nameof(UpdateBaseConfigurationAsync), baseConfiguration);
        var restartNeeded = await jobManager.StopJobs().ConfigureAwait(false);
        await teslaMateMqttService.DisconnectClient("configuration change").ConfigureAwait(false);
        await configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
        if (!configurationWrapper.GetVehicleDataFromTesla())
        {
            await teslaMateMqttService.ConnectClientIfNotConnected().ConfigureAwait(false);
        }

        if (restartNeeded)
        {
            await jobManager.StartJobs().ConfigureAwait(false);
        }
    }

    public async Task UpdateMaxCombinedCurrent(int? maxCombinedCurrent)
    {
        var baseConfiguration = await configurationWrapper.GetBaseConfigurationAsync().ConfigureAwait(false);
        baseConfiguration.MaxCombinedCurrent = maxCombinedCurrent;
        await configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
    }

    public async Task UpdatePowerBuffer(int powerBuffer)
    {
        var config = await configurationWrapper.GetBaseConfigurationAsync();
        config.PowerBuffer = powerBuffer;
        await configurationWrapper.UpdateBaseConfigurationAsync(config);
    }

    public async Task<byte[]> DownloadBackup(string backupFileNamePrefix, string? backupZipDestinationDirectory)
    {
        var destinationArchiveFileName = await CreateLocalBackupZipFile(backupFileNamePrefix, backupZipDestinationDirectory, true).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(destinationArchiveFileName).ConfigureAwait(false);
        return bytes;
    }

    public async Task<string> CreateLocalBackupZipFile(string backupFileNamePrefix, string? backupZipDestinationDirectory, bool clearBackupDirectoryBeforeBackup)
    {
        var restartNeeded = false;
        try
        {
            restartNeeded = await jobManager.StopJobs().ConfigureAwait(false);
            var backupCopyDestinationDirectory = configurationWrapper.BackupCopyDestinationDirectory();
            CreateDirectory(backupCopyDestinationDirectory);

            //Backup Sqlite database
            using (var source = new SqliteConnection(dbConnectionStringHelper.GetTeslaSolarChargerDbPath()))
            using (var destination = new SqliteConnection(string.Format($"Data Source={Path.Combine(backupCopyDestinationDirectory, configurationWrapper.GetSqliteFileNameWithoutPath())}")))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination, "main", "main");
            }

            //Backup config files
            var baseConfigFileFullName = configurationWrapper.BaseConfigFileFullName();
            File.Copy(baseConfigFileFullName, Path.Combine(backupCopyDestinationDirectory, Path.GetFileName(baseConfigFileFullName)), true);


            var backupFileName = backupFileNamePrefix + constants.BackupZipBaseFileName ;
            var backupZipDirectory = backupZipDestinationDirectory ?? configurationWrapper.BackupZipDirectory();
            if (Directory.Exists(backupZipDirectory) && clearBackupDirectoryBeforeBackup)
            {
                Directory.Delete(backupZipDirectory, true);
            }
            Directory.CreateDirectory(backupZipDirectory);
            var destinationArchiveFileName = Path.Combine(backupZipDirectory, backupFileName);
            ZipFile.CreateFromDirectory(backupCopyDestinationDirectory, destinationArchiveFileName);
            return destinationArchiveFileName;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Couldn't create backup zip file");
            throw;
        }
        finally
        {
            if (restartNeeded)
            {
                await jobManager.StartJobs().ConfigureAwait(false);
            }
        }
    }


    public async Task RestoreBackup(IFormFile file)
    {
        logger.LogTrace("{method}({file})", nameof(RestoreBackup), file.FileName);
        var jobsWereRunning = await jobManager.StopJobs().ConfigureAwait(false);
        try
        {
            var restoreTempDirectory = configurationWrapper.RestoreTempDirectory();
            CreateDirectory(restoreTempDirectory);
            var restoreFileName = "TSC-Restore.zip";
            var path = Path.Combine(restoreTempDirectory, restoreFileName);
            await using FileStream fs = new(path, FileMode.Create);
            await file.CopyToAsync(fs).ConfigureAwait(false);
            fs.Close();
            var extractedFilesDirectory = Path.Combine(restoreTempDirectory, "RestoredFiles");
            CreateDirectory(extractedFilesDirectory);
            ZipFile.ExtractToDirectory(path, extractedFilesDirectory);
            var configFileDirectoryPath = configurationWrapper.ConfigFileDirectory();
            var directoryInfo = new DirectoryInfo(configFileDirectoryPath);
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                fileInfo.Delete();
            }
            CopyFiles(extractedFilesDirectory, configFileDirectoryPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Couldn't restore backup");
            throw;
        }
        finally
        {
            settings.RestartNeeded = true;
        }
    }

    public List<DtoBackupFileInformation> GetAutoBackupFileInformations()
    {
        var backupZipDirectory = configurationWrapper.AutoBackupsZipDirectory();
        var backupFiles = Directory.GetFiles(backupZipDirectory, "*.zip");
        var backupFileInformations = new List<DtoBackupFileInformation>();
        foreach (var backupFile in backupFiles)
        {
            var fileInfo = new FileInfo(backupFile);
            backupFileInformations.Add(new DtoBackupFileInformation
            {
                FileName = fileInfo.Name,
                CreationDate = fileInfo.CreationTime,
            });
        }
        return backupFileInformations.OrderByDescending(f => f.CreationDate).ToList();
    }

    public async Task<byte[]> DownloadAutoBackup(string fileName)
    {
        var directory = configurationWrapper.AutoBackupsZipDirectory();
        var path = Path.Combine(directory, fileName);
        var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        return bytes;
    }

    private static void CreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);
    }

    private void CopyFiles(string sourceDir, string targetDir)
    {
        // Create the target directory if it doesn't already exist
        Directory.CreateDirectory(targetDir);

        // Get the files in the source directory
        var files = Directory.GetFiles(sourceDir);

        foreach (var file in files)
        {
            // Extract the file name
            var fileName = Path.GetFileName(file);

            // Combine the target directory with the file name
            var targetFilePath = Path.Combine(targetDir, fileName);

            // Copy the file
            File.Copy(file, targetFilePath, true); // true to overwrite if the file already exists
        }
    }

}
