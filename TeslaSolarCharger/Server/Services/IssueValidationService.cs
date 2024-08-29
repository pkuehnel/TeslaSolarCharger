using System.Diagnostics;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class IssueValidationService(
    ILogger<IssueValidationService> logger,
    ISettings settings,
    IPossibleIssues possibleIssues,
    IIssueKeys issueKeys,
    IConfigurationWrapper configurationWrapper,
    IConstants constants,
    IDateTimeProvider dateTimeProvider,
    ITeslaFleetApiService teslaFleetApiService)
    : IIssueValidationService
{
    public async Task<List<DtoIssue>> RefreshIssues(TimeSpan clientTimeZoneId)
    {
        logger.LogTrace("{method}()", nameof(RefreshIssues));
        var issueList = new List<DtoIssue>();
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
        issueList.AddRange(PvValueIssues());
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
        issueList.AddRange(SofwareIssues());
        return issueList;
    }

    public async Task<DtoValue<int>> ErrorCount()
    {
        logger.LogTrace("{method}()", nameof(ErrorCount));
        var issues = await RefreshIssues(TimeZoneInfo.Local.GetUtcOffset(dateTimeProvider.Now())).ConfigureAwait(false);
        var errorIssues = issues.Where(i => i.IssueSeverity == IssueSeverity.Error).ToList();
        return new DtoValue<int>(errorIssues.Count);
    }

    public async Task<DtoValue<int>> WarningCount()
    {
        logger.LogTrace("{method}()", nameof(WarningCount));
        var issues = await RefreshIssues(TimeZoneInfo.Local.GetUtcOffset(dateTimeProvider.Now())).ConfigureAwait(false);
        var warningIssues = issues.Where(i => i.IssueSeverity == IssueSeverity.Warning).ToList();
        var warningCount = new DtoValue<int>(warningIssues.Count);
        return warningCount;
    }

    private List<DtoIssue> PvValueIssues()
    {
        logger.LogTrace("{method}()", nameof(PvValueIssues));
        var issues = new List<DtoIssue>();
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

    private List<DtoIssue> SofwareIssues()
    {
        var issues = new List<DtoIssue>();
        if (settings.IsNewVersionAvailable)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.VersionNotUpToDate));
        }

        return issues;
    }

    private List<DtoIssue> GetServerConfigurationIssues(TimeSpan clientTimeUtcOffset)
    {
        var issues = new List<DtoIssue>();
        var serverTimeUtcOffset = TimeZoneInfo.Local.GetUtcOffset(dateTimeProvider.Now());
        if (clientTimeUtcOffset != serverTimeUtcOffset)
        {
            issues.Add(possibleIssues.GetIssueByKey(issueKeys.ServerTimeZoneDifferentFromClient));
        }

        return issues;
    }
}
