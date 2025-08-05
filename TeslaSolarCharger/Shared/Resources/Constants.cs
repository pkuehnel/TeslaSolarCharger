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
    public string FleetApiTokenMissingScopes => "FleetApiTokenMissingScopes";
    public string CarConfigurationsConverted => "CarConfigurationsConverted";
    public string BleBaseUrlConverted => "BleBaseUrlConverted";
    public string HandledChargesCarIdsConverted => "HandledChargesCarIdsConverted";
    public string HandledChargesConverted => "HandledChargesConverted";
    public string ChargingDetailsSolarPowerShareFixed => "ChargingDetailsSolarPowerShareFixed";
    public string SolarValuesConverted => "SolarValuesConverted";
    public TimeSpan MaxTokenRequestWaitTime => TimeSpan.FromMinutes(5);
    public TimeSpan MinTokenRestLifetime => TimeSpan.FromMinutes(2);
    public int MaxTokenUnauthorizedCount => 5;
    public int ChargingDetailsAddTriggerEveryXSeconds => 59;
    public string ChargeStartRequestUrl => "FleetApiRequests/ChargeStart";
    public string ChargeStopRequestUrl => "FleetApiRequests/ChargeStop";
    public string SetChargingAmpsRequestUrl => "FleetApiRequests/SetChargingAmps";
    public string SetChargeLimitRequestUrl => "FleetApiRequests/SetChargeLimit";
    public string WakeUpRequestUrl => "FleetApiRequests/WakeUp";
    public string VehicleRequestUrl => "FleetApiRequests/GetVehicle";
    public string VehicleDataRequestUrl => $"FleetApiRequests/GetVehicleData";
    public string TeslaTokenEncryptionKeyKey => "TeslaTokenEncryptionKey";
    public string FleetApiTokenUnauthorizedKey => "BackendTokenUnauthorized";
    public string FleetApiTokenExpirationTimeKey => "FleetApiTokenExpirationTime";
    public string FleetApiTokenStateKey => "FleetApiTokenState";
    public string BackendTokenStateKey => "BackendTokenState";
    public string IsBaseAppLicensedKey => "IsBaseAppLicensed";
    public string IsFleetApiLicensedKey => "IsFleetApiLicensed_";
    public string HomeDetectionViaConvertedKey => "HomeDetectionViaConverted";
    //Also on Cloud Server in Solar4Car.Backend.Helper.Constants
    public int FleetTelemetryReconfigurationBufferHours => 3;
    public int WeatherDateRefreshIntervallHours => 3;
    public string MeterValueEstimatesCreated => "MeterValueEstimatesCreated";
    public int MeterValueDatabaseSaveIntervalMinutes => 14;
    public int HomeBatteryMinSocRefreshIntervalMinutes => 8;
    public int WeatherPredictionInFutureDays => 7;
    public string OcppChargePointConnectorIdDelimiter => "_";
    public string DefaultIdTag => "Solar4Car";
    public string UnknownCarName => "Unknown car";
    public string PrimaryColor => "#1b6ec2";
    public string SecondaryColor => "#6c757d";
    public string SolarPowerColor => "orange";
    public string SolarPowerPredictionColor => "#FFD580";
    public string ConsumptionColor => "#ff3030";
    public string FeedInColor => "lightgreen";
    public string GridColor => "lightblue";
    public string BatteryColor => "lightsalmon";
    public string BatterySocColor => "black";
    public string EvChargingColor => "lightgrey";
    public string HomeConsumptionColor => "pink";
    public string HomeConsumptionChartColor => "deeppink";
    public string HomeConsumptionPredictionColor => "lightpink";

    public string GridPoleIcon => "<svg id=\"Layer_1\" data-name=\"Layer 1\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 83.77 122.88\"><title>power-pole</title><path d=\"M27.55,17.57h0L40.89.58A1.52,1.52,0,0,1,43,.33a1.38,1.38,0,0,1,.31.33L56.29,17.58l.09.12,24.5,11.21a1.53,1.53,0,0,1-.51,3H77.8v3.48a1.87,1.87,0,1,1-3.74,0V31.88H72.22v3.48a1.87,1.87,0,0,1-3.74,0V31.88H56.6v7.06L80.88,49.83a1.53,1.53,0,0,1-.51,3H77.8v3.71a1.87,1.87,0,1,1-3.74,0V52.8H72.22v3.71a1.87,1.87,0,1,1-3.74,0V52.8H56.6V68.11l9.7,21.43a1.53,1.53,0,0,1,.39.82.07.07,0,0,0,0,0l10.52,23.24h6.55v9.23H62.94v-9.23H73.09L44.79,92.09H38.9L10.8,113.65h10v9.23H0v-9.23H6.6L27.24,68.1V52.8H14.36v3.71a1.87,1.87,0,1,1-3.74,0V52.8h-2v3.71a1.88,1.88,0,0,1-3.75,0V52.8H3.46a1.53,1.53,0,0,1-.62-2.92l24.4-11.09V31.88H14.36v3.48a1.87,1.87,0,1,1-3.74,0V31.88h-2v3.48a1.88,1.88,0,0,1-3.75,0V31.88H3.46A1.53,1.53,0,0,1,2.82,29l24.73-11.4ZM30.3,63.91l9.93-11.38L30.52,41.38H30.3V63.91ZM41.92,50.59l8-9.21H33.9l8,9.21Zm11.42-9.21L43.62,52.53l9.91,11.38V41.38ZM41.92,54.47,31.1,66.88H52.74L41.92,54.47ZM30.3,36.63l9.31-7.79L30.3,21.05V36.63Zm11.62-9.72,7.7-6.44H34.23l7.69,6.44Zm11.61-5.83-9.29,7.77,9.29,7.79V21.08Zm-11.6,9.71-9,7.54h18l-9-7.54ZM71.64,108.7,64.11,92.09H49.82L71.64,108.7ZM33.89,92.09H19.72l-7.55,16.66L33.89,92.09Zm25.6-3L41.93,78.27,24.37,89ZM39,76.48l-9.57-5.87L22.08,86.87,39,76.48Zm2.91-1.79,7.75-4.75H34.18l7.75,4.75Zm12.46-4.06-9.55,5.85L61.73,86.84,54.39,70.63Zm2.21-41.8H73.37L56.6,21.15v7.68ZM27.24,21.07,10.42,28.83H27.24V21.07ZM56.6,42.28v7.47H73.25L56.6,42.28ZM27.24,49.75V42.14L10.51,49.75ZM52.15,17.18,42.07,4,31.74,17.18Z\"/></svg>";
    public string SunriseIcon => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\"><path d=\"M256 32l-64 80h32v64h64v-64h32l-64-80zm-9 187v80h18v-80h-18zm-63.992 53.602l-16.631 6.886 15.309 36.955 16.628-6.886-15.306-36.955zm145.984 0l-15.306 36.955 16.628 6.886 15.309-36.955-16.63-6.886zM77.795 284.068l-12.727 12.727 56.569 56.568 12.726-12.726-56.568-56.569zm356.41 0l-56.568 56.569 12.726 12.726 56.569-56.568-12.727-12.727zM256 337.994a118.919 118.919 0 0 0-59.5 15.95c-34.215 19.754-56.177 55.048-59.129 94.056H374.63c-2.952-39.008-24.914-74.302-59.129-94.057a118.919 118.919 0 0 0-59.5-15.949zM66.488 387.377l-6.886 16.63 36.955 15.307 6.886-16.628-36.955-15.309zm379.024 0l-36.955 15.309 6.886 16.628 36.955-15.306-6.886-16.631zM24 466v18h464v-18H24z\"/></svg>";
    public string SunsetIcon => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\"><path d=\"M247 27v80h18V27h-18zm-63.992 53.602l-16.631 6.886 15.309 36.955 16.628-6.886-15.306-36.955zm145.984 0l-15.306 36.955 16.628 6.886 15.309-36.955-16.63-6.886zM77.795 92.068l-12.727 12.727 56.569 56.568 12.726-12.726-56.568-56.569zm356.41 0l-56.568 56.569 12.726 12.726 56.569-56.568-12.727-12.727zM256 145.994a118.919 118.919 0 0 0-59.5 15.95c-34.215 19.754-56.177 55.048-59.129 94.056H374.63c-2.952-39.008-24.914-74.302-59.129-94.057a118.919 118.919 0 0 0-59.5-15.949zM66.488 195.377l-6.886 16.63 36.955 15.307 6.886-16.628-36.955-15.31zm379.024 0l-36.955 15.309 6.886 16.628 36.955-15.306-6.886-16.631zM24 274v18h464v-18H24zm200 62v64h-32l64 80 64-80h-32v-64h-64z\"/></svg>";

}
