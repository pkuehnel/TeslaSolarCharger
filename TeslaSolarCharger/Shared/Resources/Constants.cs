using MudBlazor;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Shared.Resources;

public class Constants : IConstants
{
    public string CarStateKey => "CarState";
    public string CarConfigurationKey => "CarConfiguration";
    public int MinSocLimit => 50;
    public int DefaultOverage => -1000000;
    public int MinimumSocDifference => 2;
    public string BackupZipBaseFileName => "TSC-Backup.zip";
    public string DefaultMargin => "mb-4";
    public Margin InputMargin => Margin.None;

    public string InstallationIdKey => "InstallationId";
    public string FleetApiTokenRequested => "FleetApiTokenRequested";
    public string TokenRefreshUnauthorized => "TokenRefreshUnauthorized";
    public string TokenMissingScopes => "TokenMissingScopes";
    public string FleetApiProxyNeeded => "FleetApiProxyNeeded";
    public string CarConfigurationsConverted => "CarConfigurationsConverted";
    public string HandledChargesCarIdsConverted => "HandledChargesCarIdsConverted";
    public string HandledChargesConverted => "HandledChargesConverted";
    public TimeSpan MaxTokenRequestWaitTime => TimeSpan.FromMinutes(5);
    public TimeSpan MinTokenRestLifetime => TimeSpan.FromMinutes(2);
    public int MaxTokenUnauthorizedCount => 5;
}
