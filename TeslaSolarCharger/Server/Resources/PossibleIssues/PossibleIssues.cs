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
                issueKeys.HomeBatteryHalfConfigured, CreateIssue("Home battery is not configured completly",
                    IssueType.Error,
                    "Did you completly configure values for Home Battery SOC and Home Battery Power?"
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
                issueKeys.HomeBatteryMinimumSocNotConfigured, CreateIssue("Although you did set settings for your home battery you did not set Home Battery Minimum Soc (%)",
                    IssueType.Error,
                    "Set the Home Battery Minimum Soc (%) in your Base Configuration"
                )
            },
            {
                issueKeys.HomeBatteryChargingPowerNotConfigured, CreateIssue("Although you did set settings for your home battery you did not set Home Battery charging power (W)",
                    IssueType.Error,
                    "Set the Home Battery charging power (W) in your Base Configuration"
                )
            },
            {
                issueKeys.VersionNotUpToDate, CreateIssue("Your installed version is not up to date.",
                    IssueType.Warning,
                    "<a href=\"https://github.com/pkuehnel/TeslaSolarCharger/releases\"  target=\"_blank\">Check release notes of latest versions</a>",
                    "Update to latest version with <code>docker-compose pull</code> and <code>docker-compose up -d</code>."
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
            PossibleSolutions = possibleSolutions,
        };
    }

    public Issue GetIssueByKey(string key)
    {
        return _issues[key];
    }
}
