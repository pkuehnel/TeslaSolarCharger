namespace TeslaSolarCharger.Shared.Enums;

public enum FleetApiTokenState
{
    NoBackendApiToken,
    BackendTokenUnauthorized,
    NoFleetApiToken,
    FleetApiTokenUnauthorized,
    FleetApiTokenMissingScopes,
    FleetApiTokenExpired,
    UpToDate,
}
