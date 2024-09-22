using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;

namespace TeslaSolarCharger.Server.Resources.PossibleIssues;

public class IssueKeys : IIssueKeys
{
    public string GridPowerNotAvailable => "GridPowerNotAvailable";
    public string InverterPowerNotAvailable => "InverterPowerNotAvailable";
    public string HomeBatterySocNotAvailable => "HomeBatterySocNotAvailable";
    public string HomeBatterySocNotPlausible => "HomeBatterySocNotPlausible";
    public string HomeBatteryPowerNotAvailable => "HomeBatteryPowerNotAvailable";
    public string HomeBatteryMinimumSocNotConfigured => "HomeBatteryMinimumSocNotConfigured";
    public string HomeBatteryChargingPowerNotConfigured => "HomeBatteryChargingPowerNotConfigured";
    public string VersionNotUpToDate => "VersionNotUpToDate";
    public string ServerTimeZoneDifferentFromClient => "ServerTimeZoneDifferentFromClient";
    public string FleetApiTokenNotRequested => "FleetApiTokenNotRequested";
    public string FleetApiTokenUnauthorized => "FleetApiTokenUnauthorized";
    public string FleetApiTokenMissingScopes => "FleetApiTokenMissingScopes";
    public string FleetApiTokenRequestExpired => "FleetApiTokenRequestExpired";
    public string FleetApiTokenNotReceived => "FleetApiTokenNotReceived";
    public string FleetApiTokenExpired => "FleetApiTokenExpired";
    public string FleetApiTokenNoApiRequestsAllowed => "FleetApiRequestsNotAllowed";
    public string CrashedOnStartup => "CrashedOnStartup";
    public string RestartNeeded => "RestartNeeded";
    public string GetVehicle => "GetVehicle";
    public string GetVehicleData => "GetVehicleData";
    public string CarStateUnknown => "CarStateUnknown";
    public string UnhandledCarStateRefresh => "UnhandledCarStateRefresh";
    public string FleetApiNonSuccessStatusCode => "FleetApiNonSuccessStatusCode_";
    public string FleetApiNonSuccessResult => "FleetApiNonSuccessResult_";
    public string UnsignedCommand => "UnsignedCommand";
    public string FleetApiTokenRefreshNonSuccessStatusCode => "FleetApiTokenRefreshNonSuccessStatusCode";
    public string CarRateLimited => "CarRateLimited";
    public string BleCommandNoSuccess => "BleCommandNoSuccess_";
    public string SolarValuesNotAvailable => "SolarValuesNotAvailable";
}
