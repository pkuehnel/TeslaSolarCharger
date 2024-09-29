using MudBlazor;

namespace TeslaSolarCharger.Shared.Resources.Contracts;

public interface IConstants
{
    string CarStateKey { get; }
    string CarConfigurationKey { get; }
    int MinSocLimit { get; }
    int DefaultOverage { get; }
    /// <summary>
    /// Soc Difference needs to be higher than this value
    /// </summary>
    int MinimumSocDifference { get; }

    string InstallationIdKey { get; }
    string FleetApiTokenRequested { get; }
    string TokenMissingScopes { get; }
    string NextAllowedTeslaApiRequest { get; }
    string BackupZipBaseFileName { get; }
    TimeSpan MaxTokenRequestWaitTime { get; }
    TimeSpan MinTokenRestLifetime { get; }
    int MaxTokenUnauthorizedCount { get; }
    string CarConfigurationsConverted { get; }
    string BleBaseUrlConverted { get; }
    string DefaultMargin { get; }
    Margin InputMargin { get; }
    string HandledChargesCarIdsConverted { get; }
    string HandledChargesConverted { get; }
    string GridPoleIcon { get; }
    int ChargingDetailsAddTriggerEveryXSeconds { get; }
    string ChargingDetailsSolarPowerShareFixed { get; }
    string SolarValuesConverted { get; }
}
