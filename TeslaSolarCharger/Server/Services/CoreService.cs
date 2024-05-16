using System.Diagnostics;
using System.Reflection;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class CoreService : ICoreService
{
    private readonly ILogger<CoreService> _logger;
    private readonly IChargingService _chargingService;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfigJsonService _configJsonService;
    private readonly JobManager _jobManager;
    private readonly ITeslaMateMqttService _teslaMateMqttService;
    private readonly ISolarMqttService _solarMqttService;
    private readonly ISettings _settings;
    private readonly IFixedPriceService _fixedPriceService;
    private readonly ITscConfigurationService _tscConfigurationService;
    private readonly IBaseConfigurationService _baseConfigurationService;
    private readonly IConstants _constants;

    public CoreService(ILogger<CoreService> logger, IChargingService chargingService, IConfigurationWrapper configurationWrapper,
        IDateTimeProvider dateTimeProvider, IConfigJsonService configJsonService, JobManager jobManager,
        ITeslaMateMqttService teslaMateMqttService, ISolarMqttService solarMqttService, ISettings settings,
        IFixedPriceService fixedPriceService, ITscConfigurationService tscConfigurationService, IBaseConfigurationService baseConfigurationService,
        IConstants constants)
    {
        _logger = logger;
        _chargingService = chargingService;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
        _configJsonService = configJsonService;
        _jobManager = jobManager;
        _teslaMateMqttService = teslaMateMqttService;
        _solarMqttService = solarMqttService;
        _settings = settings;
        _fixedPriceService = fixedPriceService;
        _tscConfigurationService = tscConfigurationService;
        _baseConfigurationService = baseConfigurationService;
        _constants = constants;
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
        var serverTimeZoneName = TimeZoneInfo.Local.IsDaylightSavingTime(_dateTimeProvider.Now())
            ? TimeZoneInfo.Local.DaylightName
            : TimeZoneInfo.Local.StandardName;
        return new DtoValue<string>(serverTimeZoneName);
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

        var destinationPath = _configurationWrapper.AutoBackupsZipDirectory();
        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
        }
        var backupFileNamePrefix = $"{currentVersion}_";

        var resultingFileName = Path.Combine(destinationPath, $"{backupFileNamePrefix + _constants.BackupZipBaseFileName}");
        if (File.Exists(resultingFileName))
        {
            _logger.LogInformation("Backup for this version already created. No new backup needed.");
            return;
        }
        await _baseConfigurationService.CreateLocalBackupZipFile(backupFileNamePrefix, destinationPath, false).ConfigureAwait(false);
    }

    private string GenerateResultFileName(string databaseFileName, string currentVersion)
    {
        var resultFileName = databaseFileName + "_" + currentVersion;
        return resultFileName;
    }

    public void LogVersion()
    {
        _logger.LogTrace("{method}()", nameof(LogVersion));
        _logger.LogInformation("Current version is {productVersion}", GetCurrentVersion().Result);
    }

    public async Task KillAllServices()
    {
        _logger.LogTrace("{method}()", nameof(KillAllServices));
        await StopJobs().ConfigureAwait(false);
        await DisconnectMqttServices().ConfigureAwait(false);
        await _configJsonService.CacheCarStates().ConfigureAwait(false);
    }

    public async Task StopJobs()
    {
        _logger.LogTrace("{method}()", nameof(StopJobs));
        await _jobManager.StopJobs().ConfigureAwait(false);
    }

    public async Task DisconnectMqttServices()
    {
        _logger.LogTrace("{method}()", nameof(DisconnectMqttServices));
        await _teslaMateMqttService.DisconnectClient("Application shutdown").ConfigureAwait(false);
        await _solarMqttService.DisconnectClient("Application shutdown").ConfigureAwait(false);
    }

    public DtoValue<int> TeslaApiRequestsSinceStartup()
    {
        _logger.LogTrace("{method}()", nameof(TeslaApiRequestsSinceStartup));
        return new DtoValue<int>(_settings.TeslaApiRequestCounter);
    }

    public DtoValue<bool> ShouldDisplayApiRequestCounter()
    {
        _logger.LogTrace("{method}()", nameof(TeslaApiRequestsSinceStartup));
        return new DtoValue<bool>(_configurationWrapper.ShouldDisplayApiRequestCounter());
    }

    public Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        _logger.LogTrace("{method}({from}, {to})", nameof(GetPriceData), from, to);
        return _fixedPriceService.GetPriceData(from, to, null);
    }

    

    public async Task<string> GetInstallationId()
    {
        var installationId = await _tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        return installationId.ToString();
    }

    public Dictionary<int, string> GetRawRestRequestResults()
    {
        return _settings.RawRestRequestResults;
    }

    public Dictionary<int, string> GetRawRestValue()
    {
        return _settings.RawRestValues;
    }

    public Dictionary<int, decimal?> GetCalculatedRestValue()
    {
        return _settings.CalculatedRestValues;
    }

    public bool IsStartupCompleted()
    {
        return _settings.IsStartupCompleted;
    }
}
