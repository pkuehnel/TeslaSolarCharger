using LanguageExt;
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
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfigJsonService _configJsonService;
    private readonly JobManager _jobManager;
    private readonly ITeslaMateMqttService _teslaMateMqttService;
    private readonly ISettings _settings;
    private readonly IFixedPriceService _fixedPriceService;
    private readonly ITscConfigurationService _tscConfigurationService;
    private readonly IBaseConfigurationService _baseConfigurationService;
    private readonly IConstants _constants;
    private readonly ITelegramService _telegramService;
    private readonly ILoadPointManagementService _loadPointManagementService;
    private readonly IPowerToControlCalculationService _powerToControlCalculationService;

    public CoreService(ILogger<CoreService> logger, IConfigurationWrapper configurationWrapper,
        IDateTimeProvider dateTimeProvider, IConfigJsonService configJsonService, JobManager jobManager,
        ITeslaMateMqttService teslaMateMqttService, ISettings settings,
        IFixedPriceService fixedPriceService, ITscConfigurationService tscConfigurationService, IBaseConfigurationService baseConfigurationService,
        IConstants constants, ITelegramService telegramService,
        ILoadPointManagementService loadPointManagementService,
        IPowerToControlCalculationService powerToControlCalculationService)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
        _configJsonService = configJsonService;
        _jobManager = jobManager;
        _teslaMateMqttService = teslaMateMqttService;
        _settings = settings;
        _fixedPriceService = fixedPriceService;
        _tscConfigurationService = tscConfigurationService;
        _baseConfigurationService = baseConfigurationService;
        _constants = constants;
        _telegramService = telegramService;
        _loadPointManagementService = loadPointManagementService;
        _powerToControlCalculationService = powerToControlCalculationService;
    }

    public Task<string?> GetCurrentVersion()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentVersion));
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        var productVersion = fileVersionInfo.ProductVersion;
        return Task.FromResult(productVersion);
    }

    public async Task<DtoValue<int>> NumberOfRelevantLoadPoints()
    {
        _logger.LogTrace("{method}()", nameof(NumberOfRelevantLoadPoints));
        var loadpointsToManage = await _loadPointManagementService.GetLoadPointsToManage().ConfigureAwait(false);
        var pluggedInLoadpointsCount = loadpointsToManage.Count(l => l.IsPluggedIn == true);
        return new DtoValue<int>(pluggedInLoadpointsCount);
    }

    public DtoValue<int> HomeBatteryTargetChargingPower()
    {
        _logger.LogTrace("{method}()", nameof(HomeBatteryTargetChargingPower));
        return new DtoValue<int>(_powerToControlCalculationService.GetBatteryTargetChargingPower());
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
        CleanupOldBackups(destinationPath, 3, 30);
        var backupFileNamePrefix = $"{currentVersion}_";

        var resultingFileName = Path.Combine(destinationPath, $"{backupFileNamePrefix + _constants.BackupZipBaseFileName}");
        if (File.Exists(resultingFileName))
        {
            _logger.LogInformation("Backup for this version already created. No new backup needed.");
            return;
        }
        await _baseConfigurationService.CreateLocalBackupZipFile(backupFileNamePrefix, destinationPath, false).ConfigureAwait(false);
    }

    private void CleanupOldBackups(string directory, int minToKeep, int daysToKeep)
    {
        _logger.LogTrace("{method}({directory}, {minToKeep}, {daysToKeep})", nameof(CleanupOldBackups), directory, minToKeep, daysToKeep);
        try
        {
            var dir = new DirectoryInfo(directory);
            if (!dir.Exists)
            {
                return;
            }

            var files = dir.GetFiles("*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
            _logger.LogTrace("Found {fileCount} backup files in directory {directory}", files.Count, directory);
            if (files.Count <= minToKeep)
            {
                return;
            }

            var cutoffUtc = _dateTimeProvider.UtcNow().AddDays(-daysToKeep);
            _logger.LogTrace("Deleting backups older than {cutoffUtc}", cutoffUtc);
            foreach (var file in files.Skip(minToKeep))
            {
                _logger.LogTrace("Checking file {fileName} with age {age}", file.Name, file.LastAccessTimeUtc);
                // Delete only if older than cutoff; otherwise keep
                if (file.LastWriteTimeUtc < cutoffUtc)
                {
                    try
                    {
                        file.Delete();
                        _logger.LogInformation("Deleted old backup: {file}", file.FullName);
                    }
                    catch (Exception ex)
                    {
                        // Don't let one failure break the whole cleanup
                        _logger.LogWarning(ex, "Failed to delete backup: {file}", file.FullName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CleanupOldBackups failed for directory {directory}", directory);
        }
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

    public async Task<Fin<DtoValue<string>>> SendTestTelegramMessage()
    {
        _logger.LogTrace("{method}()", nameof(SendTestTelegramMessage));
        var statusCode = await _telegramService.SendMessage("TeslaSolarCharger test message");
        if (((int)statusCode >= 200) && ((int)statusCode <= 299))
        {
            return Fin<DtoValue<string>>.Succ(new("Sending message succeeded"));
        }
        return Fin<DtoValue<string>>.Fail($"Sending error message failed with status code {statusCode}");
    }
}
