using System.Diagnostics;
using System.Reflection;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services;

public class CoreService : ICoreService
{
    private readonly ILogger<CoreService> _logger;
    private readonly IChargingService _chargingService;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CoreService(ILogger<CoreService> logger, IChargingService chargingService, IConfigurationWrapper configurationWrapper,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _chargingService = chargingService;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<string?> GetCurrentVersion()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentVersion));
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        return Task.FromResult(fileVersionInfo.ProductVersion);
    }

    public DtoValue<int> NumberOfRelevantCars()
    {
        _logger.LogTrace("{method}()", nameof(NumberOfRelevantCars));
        return new DtoValue<int>(_chargingService.GetRelevantCarIds().Count);
    }

    public DtoValue<int> HomeBatteryTargetChargingPower()
    {
        _logger.LogTrace("{method}()", nameof(HomeBatteryTargetChargingPower));
        return new DtoValue<int>(_chargingService.GetBatteryTargetChargingPower());
    }

    public DtoValue<bool> IsSolarEdgeInstallation()
    {
        var powerToGridUrl = _configurationWrapper.CurrentPowerToGridUrl();
        return new DtoValue<bool>(!string.IsNullOrEmpty(powerToGridUrl) && powerToGridUrl.StartsWith("http://solaredgeplugin"));
    }

    public DateTime GetCurrentServerTime()
    {
        return _dateTimeProvider.Now();
    }

    public DtoValue<string> GetServerTimeZoneDisplayName()
    {
        var serverTimeZone = TimeZoneInfo.Local;
        return new DtoValue<string>(serverTimeZone.DisplayName);
    }

    public async Task BackupDatabaseIfNeeded()
    {
        _logger.LogTrace("{method}()", nameof(BackupDatabaseIfNeeded));

        var currentVersion = await GetCurrentVersion().ConfigureAwait(false);
        if (string.IsNullOrEmpty(currentVersion))
        {
            _logger.LogWarning("Could not determine current version. Do not backup database.");
            return;
        }

        var databaseFileName = _configurationWrapper.SqliteFileFullName();
        if (!File.Exists(databaseFileName))
        {
            _logger.LogWarning("Database file does not exist. Backup is not created.");
            return;
        }

        var resultFileName = GenerateResultFileName(databaseFileName, currentVersion);
        if (File.Exists(resultFileName))
        {
            _logger.LogInformation("Database before upgrade to current version already backed up.");
            return;
        }

        File.Copy(databaseFileName, resultFileName, true);

        var shmFileName = databaseFileName + "-shm";
        if (File.Exists(shmFileName))
        {
            File.Copy(shmFileName, GenerateResultFileName(shmFileName, currentVersion), true);
        }

        var walFileName = databaseFileName + "-wal";
        if (File.Exists(walFileName))
        {
            File.Copy(walFileName, GenerateResultFileName(walFileName, currentVersion), true);
        }
    }

    private string GenerateResultFileName(string databaseFileName, string currentVersion)
    {
        var resultFileName = databaseFileName + "_" + currentVersion;
        return resultFileName;
    }

    private bool DatabaseAlreadyBackedUp()
    {

        throw new NotImplementedException();
    }

    public void LogVersion()
    {
        _logger.LogTrace("{method}()", nameof(LogVersion));
        _logger.LogInformation("Current version is {productVersion}", GetCurrentVersion().Result);
    }
}
