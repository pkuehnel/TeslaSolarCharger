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

public class IssueValidationService(
    ILogger<IssueValidationService> logger,
    ISettings settings,
    ITeslaMateMqttService teslaMateMqttService,
    IPossibleIssues possibleIssues,
    IssueKeys issueKeys,
    IConfigurationWrapper configurationWrapper,
    ITeslamateContext teslamateContext,
    IConstants constants,
    IDateTimeProvider dateTimeProvider,
    ITeslaFleetApiService teslaFleetApiService)
    : IIssueValidationService
{
    public async Task<List<Issue>> RefreshIssues(TimeSpan clientTimeZoneId)
    {
        logger.LogTrace("{method}()", nameof(RefreshIssues));
        var issueList = new List<Issue>();
        if (settings.RestartNeeded)
        {
            issueList.Add(possibleIssues.GetIssueByKey(issueKeys.RestartNeeded));
            return issueList;
        }
        if (settings.CrashedOnStartup)
        {
            var crashedOnStartupIssue = possibleIssues.GetIssueByKey(issueKeys.CrashedOnStartup);
            crashedOnStartupIssue.PossibleSolutions.Add($"Exeption Message: <code>{settings.StartupCrashMessage}</code>");
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
        if (!configurationWrapper.UseFleetApi())
        {
            issueList.AddRange(await GetTeslaMateApiIssues().ConfigureAwait(false));
        }
        else
        {
            var tokenState = (await teslaFleetApiService.GetFleetApiTokenState().ConfigureAwait(false)).Value;
            switch (tokenState)
            {
                case FleetApiTokenState.NotNeeded:
                    break;
                case FleetApiTokenState.NotRequested:
                    issueList.Add(possibleIssues.GetIssueByKey(issueKeys.FleetApiTokenNotRequested));
                    break;
                case FleetApiTokenState.TokenRequestExpired:
                    issueList.Add(possibleIssues.GetIssueByKey(issueKeys.FleetApiTokenRequestExpired));
                    break;
                case FleetApiTokenState.TokenUnauthorized:
                    issueList.Add(possibleIssues.GetIssueByKey(issueKeys.FleetApiTokenUnauthorized));
                    break;
                case FleetApiTokenState.MissingScopes:
                    issueList.Add(possibleIssues.GetIssueByKey(issueKeys.FleetApiTokenMissingScopes));
                    break;
                case FleetApiTokenState.NotReceived:
                    issueList.Add(possibleIssues.GetIssueByKey(issueKeys.FleetApiTokenNotReceived));
                    break;
                case FleetApiTokenState.Expired:
                    issueList.Add(possibleIssues.GetIssueByKey(issueKeys.FleetApiTokenExpired));
                    break;
                case FleetApiTokenState.NoApiRequestsAllowed:
                    issueList.Add(possibleIssues.GetIssueByKey(issueKeys.FleetApiTokenNoApiRequestsAllowed));
                    break;
                case FleetApiTokenState.UpToDate:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        issueList.AddRange(SofwareIssues());
        issueList.AddRange(ConfigurationIssues());
        return issueList;
    }

    public async Task<DtoValue<int>> ErrorCount()
    {
        logger.LogTrace("{method}()", nameof(ErrorCount));
        var issues = await RefreshIssues(TimeZoneInfo.Local.GetUtcOffset(dateTimeProvider.Now())).ConfigureAwait(false);
        var errorIssues = issues.Where(i => i.IssueType == IssueType.Error).ToList();
        return new DtoValue<int>(errorIssues.Count);
    }

    public async Task<DtoValue<int>> WarningCount()
    {
        logger.LogTrace("{method}()", nameof(WarningCount));
        var issues = await RefreshIssues(TimeZoneInfo.Local.GetUtcOffset(dateTimeProvider.Now())).ConfigureAwait(false);
        var warningIssues = issues.Where(i => i.IssueType == IssueType.Warning).ToList();
        var warningCount = new DtoValue<int>(warningIssues.Count);
        return warningCount;
    }

    private async Task<List<Issue>> GetDatabaseIssues()
    {
        logger.LogTrace("{method}()", nameof(GetDatabaseIssues));
        var issues = new List<Issue>();
        try
        {
            // ReSharper disable once UnusedVariable
            var carIds = await teslamateContext.Cars.Select(car => car.Id).ToListAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.DatabaseNotAvailable));
            return issues;
        }

        return issues;
    }

    private async Task<List<Issue>> GetTeslaMateApiIssues()
    {
        logger.LogTrace("{method}()", nameof(GetTeslaMateApiIssues));
        var issues = new List<Issue>();
        var teslaMateBaseUrl = configurationWrapper.TeslaMateApiBaseUrl();
        var getAllCarsUrl = $"{teslaMateBaseUrl}/api/v1/cars";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(1);
        try
        {
            var resultString = await httpClient.GetStringAsync(getAllCarsUrl).ConfigureAwait(false);
            if (string.IsNullOrEmpty(resultString))
            {
                issues.Add(possibleIssues.GetIssueByKey(issueKeys.TeslaMateApiNotAvailable));
            }
        }
        catch (Exception)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.TeslaMateApiNotAvailable));
        }
        return issues;
    }

    private List<Issue> GetMqttIssues()
    {
        logger.LogTrace("{method}()", nameof(GetMqttIssues));
        var issues = new List<Issue>();
        if (!teslaMateMqttService.IsMqttClientConnected && !configurationWrapper.GetVehicleDataFromTesla())
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.MqttNotConnected));
        }

        if (settings.CarsToManage.Any(c => (c.SocLimit == null || c.SocLimit < constants.MinSocLimit)))
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.CarSocLimitNotReadable));
        }

        if (settings.CarsToManage.Any(c => c.SoC == null))
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.CarSocNotReadable));
        }

        return issues;
    }

    private List<Issue> PvValueIssues()
    {
        logger.LogTrace("{method}()", nameof(GetMqttIssues));
        var issues = new List<Issue>();
        var frontendConfiguration = configurationWrapper.FrontendConfiguration() ?? new FrontendConfiguration();

        var isGridPowerConfigured = frontendConfiguration.GridValueSource != SolarValueSource.None;
        if (isGridPowerConfigured && settings.Overage == null)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.GridPowerNotAvailable));
        }
        var isInverterPowerConfigured = frontendConfiguration.InverterValueSource != SolarValueSource.None;
        if (isInverterPowerConfigured && settings.InverterPower == null)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.InverterPowerNotAvailable));
        }

        var isHomeBatteryConfigured = frontendConfiguration.HomeBatteryValuesSource != SolarValueSource.None;
        if (isHomeBatteryConfigured && settings.HomeBatterySoc == null)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.HomeBatterySocNotAvailable));
        }
        if (isHomeBatteryConfigured && settings.HomeBatterySoc is > 100 or < 0)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.HomeBatterySocNotPlausible));
        }

        if (isHomeBatteryConfigured && settings.HomeBatteryPower == null)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.HomeBatteryPowerNotAvailable));
        }

        if (isHomeBatteryConfigured && (configurationWrapper.HomeBatteryMinSoc() == null))
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.HomeBatteryMinimumSocNotConfigured));
        }

        if (isHomeBatteryConfigured && (configurationWrapper.HomeBatteryChargingPower() == null))
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.HomeBatteryChargingPowerNotConfigured));
        }

        return issues;
    }

    private List<Issue> SofwareIssues()
    {
        var issues = new List<Issue>();
        if (settings.IsNewVersionAvailable)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.VersionNotUpToDate));
        }

        return issues;
    }

    private List<Issue> ConfigurationIssues()
    {
        var issues = new List<Issue>();

        if (configurationWrapper.CurrentPowerToGridCorrectionFactor() == (decimal)0.0
            || configurationWrapper.HomeBatteryPowerCorrectionFactor() == (decimal)0.0
            || configurationWrapper.HomeBatterySocCorrectionFactor() == (decimal)0.0
            || configurationWrapper.CurrentInverterPowerCorrectionFactor() == (decimal)0.0
           )
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.CorrectionFactorZero));
        }
        return issues;
    }

    private List<Issue> GetServerConfigurationIssues(TimeSpan clientTimeUtcOffset)
    {
        var issues = new List<Issue>();
        var serverTimeUtcOffset = TimeZoneInfo.Local.GetUtcOffset(dateTimeProvider.Now());
        if (clientTimeUtcOffset != serverTimeUtcOffset)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.ServerTimeZoneDifferentFromClient));
        }

        return issues;
    }
}
