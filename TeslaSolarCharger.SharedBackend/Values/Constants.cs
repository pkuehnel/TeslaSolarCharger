using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.SharedBackend.Values;

public class Constants : IConstants
{
    public string CarStateKey => "CarState";
    public string CarConfigurationKey => "CarConfiguration";
    public int MinSocLimit => 50;
    public int DefaultOverage => -1000000;
    public int MinimumSocDifference => 2;
    public string BackupZipBaseFileName => "TSC-Backup.zip";

    public string InstallationIdKey => "InstallationId";
    public string FleetApiTokenRequested => "FleetApiTokenRequested";
    public string TokenRefreshUnauthorized => "TokenRefreshUnauthorized";
    public string TokenMissingScopes => "TokenMissingScopes";
    public string FleetApiProxyNeeded => "FleetApiProxyNeeded";
}
