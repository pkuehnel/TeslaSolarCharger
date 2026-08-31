namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISmartCarApiService
{
    /// <summary>
    /// Syncs TSC's SmartCar cars with the backend's connection state: creates placeholder cars for new
    /// connections (keyed on the SmartCar vehicle id), backfills the VIN once known, and reverts
    /// disconnected cars to manual. Pass <paramref name="forceRefresh"/> to bypass the token-states
    /// cache (e.g. right after the OAuth return) so changes are picked up immediately.
    /// </summary>
    Task UpdateSmartCarCarTypes(bool forceRefresh = false);
}
