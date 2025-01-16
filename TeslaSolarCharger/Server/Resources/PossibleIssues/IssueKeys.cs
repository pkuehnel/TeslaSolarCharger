using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;

namespace TeslaSolarCharger.Server.Resources.PossibleIssues;

public class IssueKeys : IIssueKeys
{
    public string VersionNotUpToDate => "VersionNotUpToDate";
    public string FleetApiTokenUnauthorized => "FleetApiTokenUnauthorized";
    public string NoFleetApiToken => "NoFleetApiToken";
    public string FleetApiTokenMissingScopes => "FleetApiTokenMissingScopes";
    public string FleetApiTokenRequestExpired => "FleetApiTokenRequestExpired";
    public string FleetApiTokenRefreshNonSuccessStatusCode => "FleetApiTokenRefreshNonSuccessStatusCode";
    public string CrashedOnStartup => "CrashedOnStartup";
    public string RestartNeeded => "RestartNeeded";
    public string GetVehicle => "GetVehicle";
    public string GetVehicleData => "GetVehicleData";
    public string CarStateUnknown => "CarStateUnknown";
    public string FleetApiNonSuccessStatusCode => "FleetApiNonSuccessStatusCode_";
    public string Solar4CarSideFleetApiNonSuccessStatusCode => "Solar4CarSideFleetApiNonSuccessStatusCode_";
    public string FleetApiNonSuccessResult => "FleetApiNonSuccessResult_";
    public string UnsignedCommand => "UnsignedCommand";
    public string BleCommandNoSuccess => "BleCommandNoSuccess_";
    public string SolarValuesNotAvailable => "SolarValuesNotAvailable";
    public string UsingFleetApiAsBleFallback => "UsingFleetApiAsBleFallback";
    public string BleVersionCompatibility => "BleVersionCompatibility";
    public string NoBackendApiToken => "NoBackendApiToken";
    public string BackendTokenUnauthorized => "BackendTokenUnauthorized";
    public string BackendTokenNotRefreshable => "BackendTokenNotRefreshable";
    public string FleetApiTokenExpired => "FleetApiTokenExpired";
    public string BaseAppNotLicensed => "BaseAppNotLicensed";
    public string FleetApiNotLicensed => "FleetApiNotLicensed";
    public string FleetTelemetryNotConnected => "FleetTelemetryNotConnected";
}

