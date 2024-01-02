using Microsoft.Data.Sqlite;
using System.IO;
using System.IO.Compression;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BaseConfigurationService : IBaseConfigurationService
{
    private readonly ILogger<BaseConfigurationService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly JobManager _jobManager;
    private readonly ITeslaMateMqttService _teslaMateMqttService;
    private readonly ISolarMqttService _solarMqttService;
    private readonly ISettings _settings;
    private readonly IPvValueService _pvValueService;
    private readonly IDbConnectionStringHelper _dbConnectionStringHelper;
    private readonly IConstants _constants;

    public BaseConfigurationService(ILogger<BaseConfigurationService> logger, IConfigurationWrapper configurationWrapper,
        JobManager jobManager, ITeslaMateMqttService teslaMateMqttService, ISolarMqttService solarMqttService,
        ISettings settings, IPvValueService pvValueService, IDbConnectionStringHelper dbConnectionStringHelper,
        IConstants constants)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _jobManager = jobManager;
        _teslaMateMqttService = teslaMateMqttService;
        _solarMqttService = solarMqttService;
        _settings = settings;
        _pvValueService = pvValueService;
        _dbConnectionStringHelper = dbConnectionStringHelper;
        _constants = constants;
    }

    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration)
    {
        _logger.LogTrace("{method}({@baseConfiguration})", nameof(UpdateBaseConfigurationAsync), baseConfiguration);
        var restartNeeded = await _jobManager.StopJobs().ConfigureAwait(false);
        await _configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
        await _teslaMateMqttService.ConnectMqttClient().ConfigureAwait(false);
        await _solarMqttService.ConnectMqttClient().ConfigureAwait(false);
        if (_configurationWrapper.FrontendConfiguration()?.GridValueSource == SolarValueSource.None)
        {
            _settings.Overage = null;
            _pvValueService.ClearOverageValues();
        }

        if (_configurationWrapper.FrontendConfiguration()?.HomeBatteryValuesSource == SolarValueSource.None)
        {
            _settings.HomeBatteryPower = null;
            _settings.HomeBatterySoc = null;
        }

        if (_configurationWrapper.FrontendConfiguration()?.InverterValueSource == SolarValueSource.None)
        {
            _settings.InverterPower = null;
        }
        _settings.PowerBuffer = null;

        if (restartNeeded)
        {
            await _jobManager.StartJobs().ConfigureAwait(false);
        }
    }

    public async Task UpdateMaxCombinedCurrent(int? maxCombinedCurrent)
    {
        var baseConfiguration = await _configurationWrapper.GetBaseConfigurationAsync().ConfigureAwait(false);
        baseConfiguration.MaxCombinedCurrent = maxCombinedCurrent;
        await _configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
    }

    public void UpdatePowerBuffer(int powerBuffer)
    {
        _settings.PowerBuffer = powerBuffer;
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
            restartNeeded = await _jobManager.StopJobs().ConfigureAwait(false);
            var backupCopyDestinationDirectory = _configurationWrapper.BackupCopyDestinationDirectory();
            CreateEmptyDirectory(backupCopyDestinationDirectory);

            //Backup Sqlite database
            using (var source = new SqliteConnection(_dbConnectionStringHelper.GetTeslaSolarChargerDbPath()))
            using (var destination = new SqliteConnection(string.Format($"Data Source={Path.Combine(backupCopyDestinationDirectory, _configurationWrapper.GetSqliteFileNameWithoutPath())};Pooling=False")))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination, "main", "main");
            }

            //Backup config files
            var baseConfigFileFullName = _configurationWrapper.BaseConfigFileFullName();
            File.Copy(baseConfigFileFullName, Path.Combine(backupCopyDestinationDirectory, Path.GetFileName(baseConfigFileFullName)), true);


            var backupFileName = _constants.BackupZipBaseFileName + backupFileNameSuffix;
            var backupZipDirectory = backupZipDestinationDirectory ?? _configurationWrapper.BackupZipDirectory();
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
            _logger.LogError(ex, "Couldn't create backup zip file");
            throw;
        }
        finally
        {
            if (restartNeeded)
            {
                await _jobManager.StartJobs().ConfigureAwait(false);
            }
        }
    }


    public async Task RestoreBackup(IFormFile file)
    {
        _logger.LogTrace("{method}({file})", nameof(RestoreBackup), file.FileName);
        var jobsWereRunning = await _jobManager.StopJobs().ConfigureAwait(false);
        try
        {
            var restoreTempDirectory = _configurationWrapper.RestoreTempDirectory();
            CreateEmptyDirectory(restoreTempDirectory);
            var restoreFileName = "TSC-Restore.zip";
            var path = Path.Combine(restoreTempDirectory, restoreFileName);
            await using FileStream fs = new(path, FileMode.Create);
            await file.CopyToAsync(fs).ConfigureAwait(false);
            fs.Close();
            var extractedFilesDirectory = Path.Combine(restoreTempDirectory, "RestoredFiles");
            CreateEmptyDirectory(extractedFilesDirectory);
            ZipFile.ExtractToDirectory(path, extractedFilesDirectory);
            var configFileDirectoryPath = _configurationWrapper.ConfigFileDirectory();
            var directoryInfo = new DirectoryInfo(configFileDirectoryPath);
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                fileInfo.Delete();
            }
            CopyFiles(extractedFilesDirectory, configFileDirectoryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't restore backup");
            throw;
        }
        finally
        {
            if (jobsWereRunning)
            {
                await _jobManager.StartJobs().ConfigureAwait(false);
            }
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
