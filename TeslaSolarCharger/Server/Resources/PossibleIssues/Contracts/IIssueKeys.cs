namespace TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;

public interface IIssueKeys
{
    string NewSoftwareAvailable { get; }
    string NewRecommendedSoftwareAvailable { get; }
    string NewRequiredSoftwareAvailable { get; }
    string FleetApiTokenUnauthorized { get; }
    string NoFleetApiToken { get; }
    string FleetApiTokenMissingScopes { get; }
    string FleetApiTokenRequestExpired { get; }
    string FleetApiTokenRefreshNonSuccessStatusCode { get; }
    string CrashedOnStartup { get; }
    string RestartNeeded { get; }
    string GetVehicle { get; }
    string GetVehicleData { get; }
    string CarStateUnknown { get; }
    string FleetApiNonSuccessStatusCode { get; }
    string FleetApiNonSuccessResult { get; }
    string UnsignedCommand { get; }
    string BleCommandNoSuccess { get; }
    string SolarValuesNotAvailable { get; }
    string UsingFleetApiAsBleFallback { get; }
    string BleVersionCompatibility { get; }
    string NoBackendApiToken { get; }
    string BackendTokenUnauthorized { get; }
    string FleetApiTokenExpired { get; }
    string Solar4CarSideFleetApiNonSuccessStatusCode { get; }
    string BackendTokenNotRefreshable { get; }
    string BaseAppNotLicensed { get; }
    string FleetApiNotLicensed { get; }
    string FleetTelemetryNotConnected { get; }
}
