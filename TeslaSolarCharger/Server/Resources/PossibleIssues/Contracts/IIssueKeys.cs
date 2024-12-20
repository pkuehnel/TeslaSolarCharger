﻿namespace TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;

public interface IIssueKeys
{
    string VersionNotUpToDate { get; }
    string FleetApiTokenNotRequested { get; }
    string FleetApiTokenUnauthorized { get; }
    string FleetApiTokenMissingScopes { get; }
    string FleetApiTokenRequestExpired { get; }
    string FleetApiTokenNotReceived { get; }
    string FleetApiTokenExpired { get; }
    string FleetApiTokenRefreshNonSuccessStatusCode { get; }
    string FleetApiTokenNoApiRequestsAllowed { get; }
    string CrashedOnStartup { get; }
    string RestartNeeded { get; }
    string GetVehicle { get; }
    string GetVehicleData { get; }
    string CarStateUnknown { get; }
    string FleetApiNonSuccessStatusCode { get; }
    string FleetApiNonSuccessResult { get; }
    string UnsignedCommand { get; }
    string CarRateLimited { get; }
    string BleCommandNoSuccess { get; }
    string SolarValuesNotAvailable { get; }
    string UsingFleetApiAsBleFallback { get; }
    string BleVersionCompatibility { get; }
}
