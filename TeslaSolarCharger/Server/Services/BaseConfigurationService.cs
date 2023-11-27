using Microsoft.Data.Sqlite;
using System.IO.Compression;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

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

    public BaseConfigurationService(ILogger<BaseConfigurationService> logger, IConfigurationWrapper configurationWrapper,
        JobManager jobManager, ITeslaMateMqttService teslaMateMqttService, ISolarMqttService solarMqttService,
        ISettings settings, IPvValueService pvValueService, IDbConnectionStringHelper dbConnectionStringHelper)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _jobManager = jobManager;
        _teslaMateMqttService = teslaMateMqttService;
        _solarMqttService = solarMqttService;
        _settings = settings;
        _pvValueService = pvValueService;
        _dbConnectionStringHelper = dbConnectionStringHelper;
    }

    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration)
    {
        _logger.LogTrace("{method}({@baseConfiguration})", nameof(UpdateBaseConfigurationAsync), baseConfiguration);
        await _jobManager.StopJobs().ConfigureAwait(false);
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

        await _jobManager.StartJobs().ConfigureAwait(false);
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

    public async Task<byte[]> DownloadBackup()
    {
        try
        {
            await _jobManager.StopJobs().ConfigureAwait(false);
            
            var backupCopyDestinationDirectory = _configurationWrapper.BackupCopyDestinationDirectory();
            if (Directory.Exists(backupCopyDestinationDirectory))
            {
                Directory.Delete(backupCopyDestinationDirectory, true);
            }
            Directory.CreateDirectory(backupCopyDestinationDirectory);

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
            var carConfigFileFullName = _configurationWrapper.CarConfigFileFullName();
            File.Copy(baseConfigFileFullName, Path.Combine(backupCopyDestinationDirectory, Path.GetFileName(baseConfigFileFullName)), true);
            File.Copy(carConfigFileFullName, Path.Combine(backupCopyDestinationDirectory, Path.GetFileName(carConfigFileFullName)), true);


            var backupFileName = "TSC-Backup.zip";
            var backupZipDirectory = _configurationWrapper.BackupZipDirectory();
            if(Directory.Exists(backupZipDirectory))
            {
                Directory.Delete(backupZipDirectory, true);
            }
            Directory.CreateDirectory(backupZipDirectory);
            var destinationArchiveFileName = Path.Combine(backupZipDirectory, backupFileName);
            ZipFile.CreateFromDirectory(backupCopyDestinationDirectory, destinationArchiveFileName);
            var bytes = await File.ReadAllBytesAsync(destinationArchiveFileName).ConfigureAwait(false);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't download backup");
            throw;
        }
        finally
        {
            await _jobManager.StartJobs().ConfigureAwait(false);
        }
        


    }
}
