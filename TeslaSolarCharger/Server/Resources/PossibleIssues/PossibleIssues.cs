using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Resources.PossibleIssues;

public class PossibleIssues : IPossibleIssues
{
    private readonly Dictionary<string, DtoIssue> _issues;

    public PossibleIssues(IIssueKeys issueKeys)
    {
        _issues = new Dictionary<string, DtoIssue>
        {
            {
                issueKeys.GridPowerNotAvailable, CreateIssue("Grid Power is not available",
                    IssueSeverity.Error,
                    "Are all settings related to grid power (url, extraction patterns, headers,...) correct?",
                    "Are there any firewall related issues preventing reading the grid power value?"
                )
            },
            {
                issueKeys.InverterPowerNotAvailable, CreateIssue("Inverter power is not available",
                    IssueSeverity.Warning,
                    "Does your inverter currently produce energy? Some inverters do not return any value if solar power is not available and you can ignore this issue.",
                    "Are all settings related to inverter power (url, extraction patterns, headers,...) correct?",
                    "Are there any firewall related issues preventing reading the inverter power value?"
                )
            },
            {
                issueKeys.HomeBatterySocNotAvailable, CreateIssue("Home battery soc is not available",
                    IssueSeverity.Error,
                    "Are all settings related to home battery soc (url, extraction patterns, headers,...) correct?",
                    "Are there any firewall related issues preventing reading the home battery soc value?"
                )
            },
            {
                issueKeys.HomeBatterySocNotPlausible, CreateIssue("Home battery soc is not plausible",
                    IssueSeverity.Error,
                    "Change the correction factor. Soc needs to be a value between 0 and 100."
                )
            },
            {
                issueKeys.HomeBatteryPowerNotAvailable, CreateIssue("Home battery power is not available",
                    IssueSeverity.Error,
                    "Are all settings related to home battery power (url, extraction patterns, headers,...) correct?",
                    "Are there any firewall related issues preventing reading the home battery power value?"
                )
            },
            {
                issueKeys.HomeBatteryMinimumSocNotConfigured, CreateIssue("Home Battery Minimum Soc (%) is not set.",
                    IssueSeverity.Error,
                    "Set the Home Battery Minimum Soc (%) in your Base Configuration"
                )
            },
            {
                issueKeys.HomeBatteryChargingPowerNotConfigured, CreateIssue("Home Battery charging power (W) is not set.",
                    IssueSeverity.Error,
                    "Set the Home Battery charging power (W) in your Base Configuration"
                )
            },
            {
                issueKeys.VersionNotUpToDate, CreateIssue("Your installed version is not up to date. Note: The first startup after an update may take more time than usual as the database format is converted. Do not stop TSC during the first startup as this might corrupt the database.",
                    IssueSeverity.Warning,
                    "<a href=\"https://github.com/pkuehnel/TeslaSolarCharger/releases\"  target=\"_blank\">Check release notes of latest versions</a>",
                    "Update to latest version with <code>docker compose pull</code> and <code>docker compose up -d</code>."
                )
            },
            {
                issueKeys.ServerTimeZoneDifferentFromClient, CreateIssue("Server time zone does not match client timezone",
                    IssueSeverity.Warning,
                    "Update the TimeZone of the TeslaSolarChargerContainer in your docker-compose.yml."
                )
            },
            {
                issueKeys.FleetApiTokenNotRequested, CreateIssue("You did not request a Tesla Token, yet.",
                    IssueSeverity.Error,
                    "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token. Important: You need to allow access to all selectable scopes."
                )
            },
            {
                issueKeys.FleetApiTokenUnauthorized, CreateIssue("Your Tesla token is unauthorized, this could be due to a changed Tesla account password, or your you disabled mobile access in your car.",
                    IssueSeverity.Error,
                    "Open the <a href=\"/BaseConfiguration\">Base Configuration</a>, request a new token and select all available scopes.",
                    "Enable mobile access in your car."
                )
            },
            {
                issueKeys.FleetApiTokenMissingScopes, CreateIssue("Your Tesla token has missing scopes.",
                    IssueSeverity.Error,
                    "Remove Tesla Solar Charger from your <a href=\"https://accounts.tesla.com/account-settings/security?tab=tpty-apps\" target=\"_blank\">third party apps</a> as you won't get asked again for the scopes. After that request a new token in the <a href=\"/BaseConfiguration\">Base Configuration</a> and select all available scopes."
                )
            },
            {
                issueKeys.FleetApiTokenNotReceived, CreateIssue("The Tesla token was not received, yet.",
                    IssueSeverity.Warning,
                    "Getting the Token can take up to five minutes after submitting your password.",
                    "If waiting five minutes does not help, open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token."
                )
            },
            {
                issueKeys.FleetApiTokenRequestExpired, CreateIssue("The Tesla token could not be received.",
                    IssueSeverity.Error,
                    "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token.",
                    "If this issue keeps occuring, feel free to open an issue <a href=\"https://github.com/pkuehnel/TeslaSolarCharger/issues\" target=\"_blank\">on Github</a> including the first 10 chars of your installation ID (bottom of the page). Do NOT include the whole ID."
                )
            },
            {
                issueKeys.FleetApiTokenExpired, CreateIssue("Your Tesla token is expired, this can occur when you changed your password or did not use the TeslaSolarCharger for too long..",
                    IssueSeverity.Error,
                    "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token."
                )
            },
            {
                issueKeys.CrashedOnStartup, CreateIssue("The application crashed while starting up.",
                    IssueSeverity.Error,
                    "Look into the logfiles for further details."
                )
            },
            {
                issueKeys.FleetApiTokenNoApiRequestsAllowed, CreateIssue("Fleet API requests are not allowed.",
                    IssueSeverity.Error,
                    "Update TSC to the latest version."
                )
            },
            {
                issueKeys.RestartNeeded, CreateIssue("A restart is needed.",
                    IssueSeverity.Error,
                    "Restart the TSC container.",
                                         "Restart the Docker host."
                )
            },
        };
    }

    private DtoIssue CreateIssue(string issueMessage, IssueSeverity issueSeverity, params string[] possibleSolutions)
    {
        return new DtoIssue()
        {
            IssueMessage = issueMessage,
            IssueSeverity = issueSeverity,
            PossibleSolutions = possibleSolutions.ToList(),
        };
    }

    public DtoIssue GetIssueByKey(string key)
    {
        return _issues[key];
    }
}
