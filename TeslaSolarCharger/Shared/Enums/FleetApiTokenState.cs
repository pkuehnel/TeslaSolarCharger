namespace TeslaSolarCharger.Shared.Enums;

public enum FleetApiTokenState
{
    NoBackendApiToken,
    BackendTokenUnauthorized,
    FleetApiTokenUnauthorized,
    FleetApiTokenMissingScopes,
    FleetApiTokenExpired,
    UpToDate,
}
