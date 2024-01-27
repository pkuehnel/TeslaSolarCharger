namespace TeslaSolarCharger.Server.Resources.PossibleIssues;

public class IssueKeys
{
    public string MqttNotConnected => "MqttNotConnected";
    public string CarSocLimitNotReadable => "CarSocLimitNotReadable";
    public string CarSocNotReadable => "CarSocNotReadable";
    public string GridPowerNotAvailable => "GridPowerNotAvailable";
    public string InverterPowerNotAvailable => "InverterPowerNotAvailable";
    public string HomeBatterySocNotAvailable => "HomeBatterySocNotAvailable";
    public string HomeBatterySocNotPlausible => "HomeBatterySocNotPlausible";
    public string HomeBatteryPowerNotAvailable => "HomeBatteryPowerNotAvailable";
    public string HomeBatteryMinimumSocNotConfigured => "HomeBatteryMinimumSocNotConfigured";
    public string HomeBatteryChargingPowerNotConfigured => "HomeBatteryChargingPowerNotConfigured";
    public string TeslaMateApiNotAvailable => "TeslaMateApiNotAvailable";
    public string DatabaseNotAvailable => "DatabaseNotAvailable";
    public string GeofenceNotAvailable => "GeofenceNotAvailable";
    public string CarIdNotAvailable => "CarIdNotAvailable";
    public string VersionNotUpToDate => "VersionNotUpToDate";
    public string CorrectionFactorZero => "CorrectionFactorZero";
    public string ServerTimeZoneDifferentFromClient => "ServerTimeZoneDifferentFromClient";
    public string FleetApiTokenNotRequested => "FleetApiTokenNotRequested";
    public string FleetApiTokenUnauthorized => "FleetApiTokenUnauthorized";
    public string FleetApiTokenMissingScopes => "FleetApiTokenMissingScopes";
    public string FleetApiTokenRequestExpired => "FleetApiTokenRequestExpired";
    public string FleetApiTokenNotReceived => "FleetApiTokenNotReceived";
    public string FleetApiTokenExpired => "FleetApiTokenExpired";
    public string FleetApiTokenNoApiRequestsAllowed => "FleetApiRequestsNotAllowed";
    public string CrashedOnStartup => "CrashedOnStartup";
}
