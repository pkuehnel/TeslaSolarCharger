using AutoMapper.QueryableExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.IO.Compression;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;
using TeslaSolarCharger.SharedBackend.MappingExtensions;

namespace TeslaSolarCharger.Server.Services;

public class BaseConfigurationService(
    ILogger<BaseConfigurationService> logger,
    IConfigurationWrapper configurationWrapper,
    JobManager jobManager,
    ITeslaMateMqttService teslaMateMqttService,
    ISolarMqttService solarMqttService,
    ISettings settings,
    IPvValueService pvValueService,
    IDbConnectionStringHelper dbConnectionStringHelper,
    IConstants constants,
    IMapperConfigurationFactory mapperConfigurationFactory,
    ITeslaSolarChargerContext teslaSolarChargerContext)
    : IBaseConfigurationService
{
    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration)
    {
        logger.LogTrace("{method}({@baseConfiguration})", nameof(UpdateBaseConfigurationAsync), baseConfiguration);
        var restartNeeded = await jobManager.StopJobs().ConfigureAwait(false);
        await configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
        if (!configurationWrapper.GetVehicleDataFromTesla())
        {
            await teslaMateMqttService.ConnectClientIfNotConnected().ConfigureAwait(false);
        }
        await solarMqttService.ConnectMqttClient().ConfigureAwait(false);
        if (configurationWrapper.FrontendConfiguration()?.GridValueSource == SolarValueSource.None)
        {
            settings.Overage = null;
            pvValueService.ClearOverageValues();
        }

        if (configurationWrapper.FrontendConfiguration()?.HomeBatteryValuesSource == SolarValueSource.None)
        {
            settings.HomeBatteryPower = null;
            settings.HomeBatterySoc = null;
        }

        if (configurationWrapper.FrontendConfiguration()?.InverterValueSource == SolarValueSource.None)
        {
            settings.InverterPower = null;
        }
        settings.PowerBuffer = null;

        if (restartNeeded)
        {
            await jobManager.StartJobs().ConfigureAwait(false);
        }
    }

    public async Task<List<DtoRestConfigurationOverview>> GetRestValueConfigurations()
    {
        logger.LogTrace("{method}()", nameof(GetRestValueConfigurations));
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<RestValueConfiguration, DtoRestConfigurationOverview>()
                .ForMember(d => d.Results, opt => opt.MapFrom(s => s.RestValueResultConfigurations))
                ;

            cfg.CreateMap<RestValueResultConfiguration, DtoRestValueResult>()
                .ForMember(d => d.CalculatedValue, opt => opt.Ignore())
                ;
        });

        var restValueConfigurations = await teslaSolarChargerContext.RestValueConfigurations
            .ProjectTo<DtoRestConfigurationOverview>(mapper)
            .ToListAsync();

        foreach (var restValueConfiguration in restValueConfigurations)
        {
            foreach (var dtoRestValueResult in restValueConfiguration.Results)
            {
                dtoRestValueResult.CalculatedValue = settings.CalculatedRestValues[dtoRestValueResult.Id];
            }
        }

        return restValueConfigurations;
    }

    public async Task UpdateMaxCombinedCurrent(int? maxCombinedCurrent)
    {
        var baseConfiguration = await configurationWrapper.GetBaseConfigurationAsync().ConfigureAwait(false);
        baseConfiguration.MaxCombinedCurrent = maxCombinedCurrent;
        await configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
    }

    public void UpdatePowerBuffer(int powerBuffer)
    {
        settings.PowerBuffer = powerBuffer;
    }

    public async Task<byte[]> DownloadBackup(string backupFileNameSuffix, string? backupZipDestinationDirectory)
    {
        var destinationArchiveFileName = await CreateLocalBackupZipFile(backupFileNameSuffix, backupZipDestinationDirectory).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(destinationArchiveFileName).ConfigureAwait(false);
        return bytes;
    }

    public async Task<string> CreateLocalBackupZipFile(string backupFileNameSuffix, string? backupZipDestinationDirectory)
    {
        var restartNeeded = false;
        try
        {
            restartNeeded = await jobManager.StopJobs().ConfigureAwait(false);
            var backupCopyDestinationDirectory = configurationWrapper.BackupCopyDestinationDirectory();
            CreateEmptyDirectory(backupCopyDestinationDirectory);

            //Backup Sqlite database
            using (var source = new SqliteConnection(dbConnectionStringHelper.GetTeslaSolarChargerDbPath()))
            using (var destination = new SqliteConnection(string.Format($"Data Source={Path.Combine(backupCopyDestinationDirectory, configurationWrapper.GetSqliteFileNameWithoutPath())};Pooling=False")))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination, "main", "main");
            }

            //Backup config files
            var baseConfigFileFullName = configurationWrapper.BaseConfigFileFullName();
            File.Copy(baseConfigFileFullName, Path.Combine(backupCopyDestinationDirectory, Path.GetFileName(baseConfigFileFullName)), true);


            var backupFileName = constants.BackupZipBaseFileName + backupFileNameSuffix;
            var backupZipDirectory = backupZipDestinationDirectory ?? configurationWrapper.BackupZipDirectory();
            if (Directory.Exists(backupZipDirectory))
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
            CreateEmptyDirectory(restoreTempDirectory);
            var restoreFileName = "TSC-Restore.zip";
            var path = Path.Combine(restoreTempDirectory, restoreFileName);
            await using FileStream fs = new(path, FileMode.Create);
            await file.CopyToAsync(fs).ConfigureAwait(false);
            fs.Close();
            var extractedFilesDirectory = Path.Combine(restoreTempDirectory, "RestoredFiles");
            CreateEmptyDirectory(extractedFilesDirectory);
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

    private static void CreateEmptyDirectory(string path)
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
