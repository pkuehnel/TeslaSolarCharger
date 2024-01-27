namespace TeslaSolarCharger.Shared.Enums;

public enum FleetApiTokenState
{
    NotNeeded,
    NotRequested,
    TokenRequestExpired,
    TokenUnauthorized,
    MissingScopes,
    NotReceived,
    Expired,
    UpToDate,
    NoApiRequestsAllowed,
}
