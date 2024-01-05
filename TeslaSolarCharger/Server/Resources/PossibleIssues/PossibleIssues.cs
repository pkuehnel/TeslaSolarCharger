using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Resources.PossibleIssues;

public class PossibleIssues : IPossibleIssues
{
    private readonly Dictionary<string, Issue> _issues;

    public PossibleIssues(IssueKeys issueKeys)
    {
        _issues = new Dictionary<string, Issue>
        {
            {
                issueKeys.MqttNotConnected, CreateIssue("Mqtt Client is not connected",
                    IssueType.Error,
                    "Is Mosquitto service running?",
                    "Is the Mosquitto service name in your docker-compose.yml the same as configured in the Base Configuration Page?"
                )
            },
            {
                issueKeys.CarSocLimitNotReadable, CreateIssue("Charging limit of at least one car is not available",
                    IssueType.Error,
                    "Restart TeslaMate container",
                    "Wake up cars via Tesla App",
                    "Change Charging limit of cars",
                    "Are all car IDs configured in Base Configuration available in your Tesla Account?"
                )
            },
            {
                issueKeys.CarSocNotReadable, CreateIssue("SoC of at least one car is not available",
                    IssueType.Error,
                    "Is the database running? If not start the database container and then restart TeslaSolarCharger.",
                    "Restart TeslaMate container",
                    "Wake up cars via Tesla App",
                    "Are all car IDs configured in Base Configuration available in your Tesla Account?"
                )
            },
            {
                issueKeys.GridPowerNotAvailable, CreateIssue("Grid Power is not available",
                    IssueType.Error,
                    "If you do not use solar values set grid source to none.",
                    "Are all settings related to grid power (url, extraction patterns, headers,...) correct?",
                    "Are there any firewall related issues preventing reading the grid power value?"
                )
            },
            {
                issueKeys.InverterPowerNotAvailable, CreateIssue("Inverter power is not available",
                    IssueType.Warning,
                    "Does your inverter currently produce energy? Some inverters do not return any value if solar power is not available and you can ignore this issue.",
                    "Are all settings related to inverter power (url, extraction patterns, headers,...) correct?",
                    "Are there any firewall related issues preventing reading the inverter power value?"
                )
            },
            {
                issueKeys.HomeBatterySocNotAvailable, CreateIssue("Home battery soc is not available",
                    IssueType.Error,
                    "Are all settings related to home battery soc (url, extraction patterns, headers,...) correct?",
                    "Are there any firewall related issues preventing reading the home battery soc value?"
                )
            },
            {
                issueKeys.HomeBatterySocNotPlausible, CreateIssue("Home battery soc is not plausible",
                    IssueType.Error,
                    "Change the correction factor. Soc needs to be a value between 0 and 100."
                )
            },
            {
                issueKeys.HomeBatteryPowerNotAvailable, CreateIssue("Home battery power is not available",
                    IssueType.Error,
                    "Are all settings related to home battery power (url, extraction patterns, headers,...) correct?",
                    "Are there any firewall related issues preventing reading the home battery power value?"
                )
            },
            {
                issueKeys.TeslaMateApiNotAvailable, CreateIssue("Could not access TeslaMateApi",
                    IssueType.Error,
                    "Is the TeslaMateApi container running",
                    "Is the TeslaMateApi service name in your docker-compose.yml the same as configured in the Base Configuration Page?"
                )
            },
            {
                issueKeys.DatabaseNotAvailable, CreateIssue("Could not access Database",
                    IssueType.Warning,
                    "Is the database container running",
                    "Is the database service name in your docker-compose.yml the same as configured in the Base Configuration Page?",
                    "Did you change the database username or password in your docker-compose.yml and not update it in the Base Configuration Page?"
                )
            },
            {
                issueKeys.GeofenceNotAvailable, CreateIssue("Configured Geofence is not available in TeslaMate. This results in TeslaSolarCharger never set anything",
                    IssueType.Warning,
                    "Add a geofence with the same name as configured in your Base Configuration to TeslaMate."
                )
            },
            {
                issueKeys.CarIdNotAvailable, CreateIssue("At least one of your configured car IDs is not available in TeslaMate.",
                    IssueType.Error,
                    "Update the car IDs in your Base Configuration to existing car IDs in TeslaMate."
                )
            },
            {
                issueKeys.HomeBatteryMinimumSocNotConfigured, CreateIssue("Home Battery Minimum Soc (%) is not set.",
                    IssueType.Error,
                    "Set the Home Battery Minimum Soc (%) in your Base Configuration"
                )
            },
            {
                issueKeys.HomeBatteryChargingPowerNotConfigured, CreateIssue("Home Battery charging power (W) is not set.",
                    IssueType.Error,
                    "Set the Home Battery charging power (W) in your Base Configuration"
                )
            },
            {
                issueKeys.VersionNotUpToDate, CreateIssue("Your installed version is not up to date. Note: The first startup after an update may take more time than usual as the database format is converted. Do not stop TSC during the first startup as this might corrupt the database.",
                    IssueType.Warning,
                    "<a href=\"https://github.com/pkuehnel/TeslaSolarCharger/releases\"  target=\"_blank\">Check release notes of latest versions</a>",
                    "Update to latest version with <code>docker compose pull</code> and <code>docker compose up -d</code>."
                )
            },
            {
                issueKeys.CorrectionFactorZero, CreateIssue("At least one correction factor is set to 0, resulting in the result always being 0.",
                    IssueType.Error,
                    "Set the grid power correction factor to a value different than 0.",
                    "Set the inverter power correction factor to a value different than 0.",
                    "Set the home battery power correction factor to a value different than 0.",
                    "Set the home batter SoC correction factor to a value different than 0."
                )
            },
            {
                issueKeys.ServerTimeZoneDifferentFromClient, CreateIssue("Server time zone does not match client timezone",
                    IssueType.Warning,
                    "Update the TimeZone of the TeslaSolarChargerContainer in your docker-compose.yml."
                )
            },
            {
                issueKeys.NewTeslaApiNotUsed, CreateIssue("New cars need a new Tesla API. As this is in a very early beta state I highly recommend not using it if your car supports the old API!",
                    IssueType.Information,
                    "To use the new API add <code>UseFleetApi=true</code> as environment variable in your <code>docker-compose.yml</code>",
                    "Sorry for this information beeing not removable - as switching to the new API in January 2024 as default you won't see this information from then on."
                )
            },
            {
                issueKeys.FleetApiTokenNotRequested, CreateIssue("You did not request a Tesla Token, yet.",
                    IssueType.Error,
                    "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token. Important: You need to allow access to all selectable scopes."
                )
            },
            {
                issueKeys.FleetApiTokenUnauthorized, CreateIssue("Your Tesla token is unauthorized, this could be due to a changed Tesla account password, or your you disabled mobile access in your car.",
                    IssueType.Error,
                    "Open the <a href=\"/BaseConfiguration\">Base Configuration</a>, request a new token and select all available scopes.",
                    "Enable mobile access in your car."
                )
            },
            {
                issueKeys.FleetApiTokenMissingScopes, CreateIssue("Your Tesla token has missing scopes.",
                    IssueType.Error,
                    "Remove Tesla Solar Charger from your <a href=\"https://accounts.tesla.com/account-settings/security?tab=tpty-apps\" target=\"_blank\">third party apps</a> as you won't get asked again for the scopes. After that request a new token in the <a href=\"/BaseConfiguration\">Base Configuration</a> and select all available scopes."
                )
            },
            {
                issueKeys.FleetApiTokenNotReceived, CreateIssue("The Tesla token was not received, yet.",
                    IssueType.Warning,
                    "Getting the Token can take up to five minutes after submitting your password.",
                    "If waiting five minutes does not help, open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token."
                )
            },
            {
                issueKeys.FleetApiTokenRequestExpired, CreateIssue("The Tesla token could not be received.",
                    IssueType.Error,
                    "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token.",
                    "If this issue keeps occuring, feel free to open an issue <a href=\"https://github.com/pkuehnel/TeslaSolarCharger/issues\" target=\"_blank\">on Github</a> including the last 5 chars of your installation ID (bottom of the page). Do NOT include the whole ID."
                )
            },
            {
                issueKeys.FleetApiTokenExpired, CreateIssue("Your Tesla token has expired, this can occur when you changed your password or did not use the TeslaSolarCharger for too long..",
                    IssueType.Error,
                    "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token."
                )
            },
            {
                issueKeys.CrashedOnStartup, CreateIssue("The application crashed while starting up.",
                    IssueType.Error,
                    "Look into the logfiles for further details."
                )
            },
        };
    }

    private Issue CreateIssue(string issueMessage, IssueType issueType, params string[] possibleSolutions)
    {
        return new Issue()
        {
            IssueMessage = issueMessage,
            IssueType = issueType,
            PossibleSolutions = possibleSolutions.ToList(),
        };
    }

    public Issue GetIssueByKey(string key)
    {
        return _issues[key];
    }
}
