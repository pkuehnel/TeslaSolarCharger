using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net.Sockets;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class IssueValidationService : IIssueValidationService
{
    private readonly ILogger<IssueValidationService> _logger;
    private readonly ISettings _settings;
    private readonly ITeslaMateMqttService _teslaMateMqttService;
    private readonly IPossibleIssues _possibleIssues;
    private readonly IssueKeys _issueKeys;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IConstants _constants;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITeslaFleetApiService _teslaFleetApiService;

    public IssueValidationService(ILogger<IssueValidationService> logger, ISettings settings,
        ITeslaMateMqttService teslaMateMqttService, IPossibleIssues possibleIssues, IssueKeys issueKeys,
        IConfigurationWrapper configurationWrapper, ITeslamateContext teslamateContext,
        IConstants constants, IDateTimeProvider dateTimeProvider, ITeslaFleetApiService teslaFleetApiService)
    {
        _logger = logger;
        _settings = settings;
        _teslaMateMqttService = teslaMateMqttService;
        _possibleIssues = possibleIssues;
        _issueKeys = issueKeys;
        _configurationWrapper = configurationWrapper;
        _teslamateContext = teslamateContext;
        _constants = constants;
        _dateTimeProvider = dateTimeProvider;
        _teslaFleetApiService = teslaFleetApiService;
    }

    public async Task<List<Issue>> RefreshIssues(TimeSpan clientTimeZoneId)
    {
        _logger.LogTrace("{method}()", nameof(RefreshIssues));
        var issueList = new List<Issue>();
        if (_settings.CrashedOnStartup)
        {
            var crashedOnStartupIssue = _possibleIssues.GetIssueByKey(_issueKeys.CrashedOnStartup);
            crashedOnStartupIssue.PossibleSolutions.Add($"Exeption Message: <code>{_settings.StartupCrashMessage}</code>");
            issueList.Add(crashedOnStartupIssue);
            return issueList;
        }
        issueList.AddRange(GetServerConfigurationIssues(clientTimeZoneId));
        if (Debugger.IsAttached)
        {
            //return issueList;
        }
        issueList.AddRange(GetMqttIssues());
        issueList.AddRange(PvValueIssues());
        if (!_configurationWrapper.UseFleetApi())
        {
            issueList.AddRange(await GetTeslaMateApiIssues().ConfigureAwait(false));
        }
        else
        {
            var tokenState = (await _teslaFleetApiService.GetFleetApiTokenState().ConfigureAwait(false)).Value;
            switch (tokenState)
            {
                case FleetApiTokenState.NotNeeded:
                    break;
                case FleetApiTokenState.NotRequested:
                    issueList.Add(_possibleIssues.GetIssueByKey(_issueKeys.FleetApiTokenNotRequested));
                    break;
                case FleetApiTokenState.TokenRequestExpired:
                    issueList.Add(_possibleIssues.GetIssueByKey(_issueKeys.FleetApiTokenRequestExpired));
                    break;
                case FleetApiTokenState.TokenUnauthorized:
                    issueList.Add(_possibleIssues.GetIssueByKey(_issueKeys.FleetApiTokenUnauthorized));
                    break;
                case FleetApiTokenState.MissingScopes:
                    issueList.Add(_possibleIssues.GetIssueByKey(_issueKeys.FleetApiTokenMissingScopes));
                    break;
                case FleetApiTokenState.NotReceived:
                    issueList.Add(_possibleIssues.GetIssueByKey(_issueKeys.FleetApiTokenNotReceived));
                    break;
                case FleetApiTokenState.Expired:
                    issueList.Add(_possibleIssues.GetIssueByKey(_issueKeys.FleetApiTokenExpired));
                    break;
                case FleetApiTokenState.NoApiRequestsAllowed:
                    issueList.Add(_possibleIssues.GetIssueByKey(_issueKeys.FleetApiTokenNoApiRequestsAllowed));
                    break;
                case FleetApiTokenState.UpToDate:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        issueList.AddRange(await GetDatabaseIssues().ConfigureAwait(false));
        issueList.AddRange(SofwareIssues());
        issueList.AddRange(ConfigurationIssues());
        return issueList;
    }

    public async Task<DtoValue<int>> ErrorCount()
    {
        _logger.LogTrace("{method}()", nameof(ErrorCount));
        var issues = await RefreshIssues(TimeZoneInfo.Local.GetUtcOffset(_dateTimeProvider.Now())).ConfigureAwait(false);
        var errorIssues = issues.Where(i => i.IssueType == IssueType.Error).ToList();
        return new DtoValue<int>(errorIssues.Count);
    }

    public async Task<DtoValue<int>> WarningCount()
    {
        _logger.LogTrace("{method}()", nameof(WarningCount));
        var issues = await RefreshIssues(TimeZoneInfo.Local.GetUtcOffset(_dateTimeProvider.Now())).ConfigureAwait(false);
        var warningIssues = issues.Where(i => i.IssueType == IssueType.Warning).ToList();
        var warningCount = new DtoValue<int>(warningIssues.Count);
        return warningCount;
    }

    private async Task<List<Issue>> GetDatabaseIssues()
    {
        _logger.LogTrace("{method}()", nameof(GetDatabaseIssues));
        var issues = new List<Issue>();
        try
        {
            // ReSharper disable once UnusedVariable
            var carIds = await _teslamateContext.Cars.Select(car => car.Id).ToListAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.DatabaseNotAvailable));
            return issues;
        }

        var geofenceNames = _teslamateContext.Geofences.Select(ge => ge.Name).ToList();
        var configuredGeofence = _configurationWrapper.GeoFence();
        if (!geofenceNames.Any(g => g == configuredGeofence))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.GeofenceNotAvailable));
        }

        return issues;
    }

    private async Task<List<Issue>> GetTeslaMateApiIssues()
    {
        _logger.LogTrace("{method}()", nameof(GetTeslaMateApiIssues));
        var issues = new List<Issue>();
        var teslaMateBaseUrl = _configurationWrapper.TeslaMateApiBaseUrl();
        var getAllCarsUrl = $"{teslaMateBaseUrl}/api/v1/cars";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(1);
        try
        {
            var resultString = await httpClient.GetStringAsync(getAllCarsUrl).ConfigureAwait(false);
            if (string.IsNullOrEmpty(resultString))
            {
                issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.TeslaMateApiNotAvailable));
            }
        }
        catch (Exception)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.TeslaMateApiNotAvailable));
        }
        return issues;
    }

    private List<Issue> GetMqttIssues()
    {
        _logger.LogTrace("{method}()", nameof(GetMqttIssues));
        var issues = new List<Issue>();
        if (!_teslaMateMqttService.IsMqttClientConnected && !_configurationWrapper.GetVehicleDataFromTesla())
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.MqttNotConnected));
        }

        if (_settings.CarsToManage.Any(c => (c.CarState.SocLimit == null || c.CarState.SocLimit < _constants.MinSocLimit)))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.CarSocLimitNotReadable));
        }

        if (_settings.CarsToManage.Any(c => c.CarState.SoC == null))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.CarSocNotReadable));
        }

        return issues;
    }

    private List<Issue> PvValueIssues()
    {
        _logger.LogTrace("{method}()", nameof(GetMqttIssues));
        var issues = new List<Issue>();
        var frontendConfiguration = _configurationWrapper.FrontendConfiguration() ?? new FrontendConfiguration();

        var isGridPowerConfigured = frontendConfiguration.GridValueSource != SolarValueSource.None;
        if (isGridPowerConfigured && _settings.Overage == null)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.GridPowerNotAvailable));
        }
        var isInverterPowerConfigured = frontendConfiguration.InverterValueSource != SolarValueSource.None;
        if (isInverterPowerConfigured && _settings.InverterPower == null)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.InverterPowerNotAvailable));
        }

        var isHomeBatteryConfigured = frontendConfiguration.HomeBatteryValuesSource != SolarValueSource.None;
        if (isHomeBatteryConfigured && _settings.HomeBatterySoc == null)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.HomeBatterySocNotAvailable));
        }
        if (isHomeBatteryConfigured && _settings.HomeBatterySoc is > 100 or < 0)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.HomeBatterySocNotPlausible));
        }

        if (isHomeBatteryConfigured && _settings.HomeBatteryPower == null)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.HomeBatteryPowerNotAvailable));
        }

        if (isHomeBatteryConfigured && (_configurationWrapper.HomeBatteryMinSoc() == null))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.HomeBatteryMinimumSocNotConfigured));
        }

        if (isHomeBatteryConfigured && (_configurationWrapper.HomeBatteryChargingPower() == null))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.HomeBatteryChargingPowerNotConfigured));
        }

        return issues;
    }

    private List<Issue> SofwareIssues()
    {
        var issues = new List<Issue>();
        if (_settings.IsNewVersionAvailable)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.VersionNotUpToDate));
        }

        return issues;
    }

    private List<Issue> ConfigurationIssues()
    {
        var issues = new List<Issue>();

        if (_configurationWrapper.CurrentPowerToGridCorrectionFactor() == (decimal)0.0
            || _configurationWrapper.HomeBatteryPowerCorrectionFactor() == (decimal)0.0
            || _configurationWrapper.HomeBatterySocCorrectionFactor() == (decimal)0.0
            || _configurationWrapper.CurrentInverterPowerCorrectionFactor() == (decimal)0.0
           )
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.CorrectionFactorZero));
        }
        return issues;
    }

    private List<Issue> GetServerConfigurationIssues(TimeSpan clientTimeUtcOffset)
    {
        var issues = new List<Issue>();
        var serverTimeUtcOffset = TimeZoneInfo.Local.GetUtcOffset(_dateTimeProvider.Now());
        if (clientTimeUtcOffset != serverTimeUtcOffset)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.ServerTimeZoneDifferentFromClient));
        }

        return issues;
    }
}
