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

    public string NextAllowedTeslaApiRequest => "NextAllowedTeslaApiRequest";

    public string BackupZipBaseFileName => "TSC-Backup.zip";

    public string DefaultMargin => "mb-4";
    public Margin InputMargin => Margin.Dense;

    public string InstallationIdKey => "InstallationId";
    public string FleetApiTokenRequested => "FleetApiTokenRequested";
    public string TokenRefreshUnauthorized => "TokenRefreshUnauthorized";
    public string TokenMissingScopes => "TokenMissingScopes";
    public string CarConfigurationsConverted => "CarConfigurationsConverted";
    public string BleBaseUrlConverted => "BleBaseUrlConverted";
    public string HandledChargesCarIdsConverted => "HandledChargesCarIdsConverted";
    public string HandledChargesConverted => "HandledChargesConverted";
    public string ChargingDetailsSolarPowerShareFixed => "ChargingDetailsSolarPowerShareFixed";
    public TimeSpan MaxTokenRequestWaitTime => TimeSpan.FromMinutes(5);
    public TimeSpan MinTokenRestLifetime => TimeSpan.FromMinutes(2);
    public int MaxTokenUnauthorizedCount => 5;
    public int ChargingDetailsAddTriggerEveryXSeconds => 59;

    public string GridPoleIcon => "<svg id=\"Layer_1\" data-name=\"Layer 1\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 83.77 122.88\"><title>power-pole</title><path d=\"M27.55,17.57h0L40.89.58A1.52,1.52,0,0,1,43,.33a1.38,1.38,0,0,1,.31.33L56.29,17.58l.09.12,24.5,11.21a1.53,1.53,0,0,1-.51,3H77.8v3.48a1.87,1.87,0,1,1-3.74,0V31.88H72.22v3.48a1.87,1.87,0,0,1-3.74,0V31.88H56.6v7.06L80.88,49.83a1.53,1.53,0,0,1-.51,3H77.8v3.71a1.87,1.87,0,1,1-3.74,0V52.8H72.22v3.71a1.87,1.87,0,1,1-3.74,0V52.8H56.6V68.11l9.7,21.43a1.53,1.53,0,0,1,.39.82.07.07,0,0,0,0,0l10.52,23.24h6.55v9.23H62.94v-9.23H73.09L44.79,92.09H38.9L10.8,113.65h10v9.23H0v-9.23H6.6L27.24,68.1V52.8H14.36v3.71a1.87,1.87,0,1,1-3.74,0V52.8h-2v3.71a1.88,1.88,0,0,1-3.75,0V52.8H3.46a1.53,1.53,0,0,1-.62-2.92l24.4-11.09V31.88H14.36v3.48a1.87,1.87,0,1,1-3.74,0V31.88h-2v3.48a1.88,1.88,0,0,1-3.75,0V31.88H3.46A1.53,1.53,0,0,1,2.82,29l24.73-11.4ZM30.3,63.91l9.93-11.38L30.52,41.38H30.3V63.91ZM41.92,50.59l8-9.21H33.9l8,9.21Zm11.42-9.21L43.62,52.53l9.91,11.38V41.38ZM41.92,54.47,31.1,66.88H52.74L41.92,54.47ZM30.3,36.63l9.31-7.79L30.3,21.05V36.63Zm11.62-9.72,7.7-6.44H34.23l7.69,6.44Zm11.61-5.83-9.29,7.77,9.29,7.79V21.08Zm-11.6,9.71-9,7.54h18l-9-7.54ZM71.64,108.7,64.11,92.09H49.82L71.64,108.7ZM33.89,92.09H19.72l-7.55,16.66L33.89,92.09Zm25.6-3L41.93,78.27,24.37,89ZM39,76.48l-9.57-5.87L22.08,86.87,39,76.48Zm2.91-1.79,7.75-4.75H34.18l7.75,4.75Zm12.46-4.06-9.55,5.85L61.73,86.84,54.39,70.63Zm2.21-41.8H73.37L56.6,21.15v7.68ZM27.24,21.07,10.42,28.83H27.24V21.07ZM56.6,42.28v7.47H73.25L56.6,42.28ZM27.24,49.75V42.14L10.51,49.75ZM52.15,17.18,42.07,4,31.74,17.18Z\"/></svg>";
}
