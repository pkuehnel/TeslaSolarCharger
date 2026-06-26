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
    public string TeslasAddedToAllowedCars => "TeslasAddedToAllowedCars";
    public string ChargingDetailsSolarPowerShareFixed => "ChargingDetailsSolarPowerShareFixed";
    public string SolarValuesConverted => "SolarValuesConverted";
    public TimeSpan MaxTokenRequestWaitTime => TimeSpan.FromMinutes(5);
    public TimeSpan MinTokenRestLifetime => TimeSpan.FromMinutes(2);
    public int MaxTokenUnauthorizedCount => 5;
    public int ChargingDetailsAddTriggerEveryXSeconds => 11;
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
    public string SmartCarTokenStatesKey => "SmartCarTokenStates";
    public string IsBaseAppLicensedKey => "IsBaseAppLicensed";
    public string IsFleetApiLicensedKey => "IsFleetApiLicensed_";
    public string SetupCacheKey => "SetupCache";
    public string HomeDetectionViaConvertedKey => "HomeDetectionViaConverted";
    //Also on Cloud Server in Solar4Car.Backend.Helper.Constants
    public int FleetTelemetryReconfigurationBufferHours => 3;
    public int WeatherDateRefreshIntervallHours => 3;
    public string MeterValueEstimatesCreated => "MeterValueEstimatesCreated";
    public int MeterValueDatabaseSaveIntervalMinutes => 6;
    public int HomeBatteryMinSocRefreshIntervalMinutes => 8;
    public int WeatherPredictionInFutureDays => 7;
    public int CarCapabilityMaxCurrentAboveMeasuredCurrent => 2;
    public string OcppChargePointConnectorIdDelimiter => "_";
    public string DefaultIdTag => "Solar4Car";
    public string UnknownCarName => "Guest car";

    public string GridPoleIcon => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"currentColor\"><ellipse cx=\"5.5\" cy=\"4.3\" rx=\"1.4\" ry=\"0.9\"/><ellipse cx=\"5.5\" cy=\"2.9\" rx=\"1.05\" ry=\"0.75\"/><ellipse cx=\"12\" cy=\"4.3\" rx=\"1.4\" ry=\"0.9\"/><ellipse cx=\"12\" cy=\"2.9\" rx=\"1.05\" ry=\"0.75\"/><ellipse cx=\"18.5\" cy=\"4.3\" rx=\"1.4\" ry=\"0.9\"/><ellipse cx=\"18.5\" cy=\"2.9\" rx=\"1.05\" ry=\"0.75\"/><rect x=\"3.5\" y=\"5.2\" width=\"17\" height=\"1.7\" rx=\"0.3\"/><rect x=\"11\" y=\"5.2\" width=\"2\" height=\"15.6\"/><polygon points=\"4.71,6.39 5.29,7.21 11.29,11.41 10.71,10.59\"/><polygon points=\"19.29,6.39 18.71,7.21 12.71,11.41 13.29,10.59\"/><rect x=\"8.5\" y=\"20.8\" width=\"7\" height=\"2.2\" rx=\"0.3\"/></svg>";
    public string SolarPowerIcon => Icons.Material.Filled.WbSunny;
    public string HomeBatteryIcon => Icons.Material.Filled.BatteryChargingFull;
    public string HomePowerIcon => Icons.Material.Filled.Home;
    public string EvPowerIcon => Icons.Material.Filled.EvStation;
    public string SunriseIcon => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\"><path d=\"M256 32l-64 80h32v64h64v-64h32l-64-80zm-9 187v80h18v-80h-18zm-63.992 53.602l-16.631 6.886 15.309 36.955 16.628-6.886-15.306-36.955zm145.984 0l-15.306 36.955 16.628 6.886 15.309-36.955-16.63-6.886zM77.795 284.068l-12.727 12.727 56.569 56.568 12.726-12.726-56.568-56.569zm356.41 0l-56.568 56.569 12.726 12.726 56.569-56.568-12.727-12.727zM256 337.994a118.919 118.919 0 0 0-59.5 15.95c-34.215 19.754-56.177 55.048-59.129 94.056H374.63c-2.952-39.008-24.914-74.302-59.129-94.057a118.919 118.919 0 0 0-59.5-15.949zM66.488 387.377l-6.886 16.63 36.955 15.307 6.886-16.628-36.955-15.309zm379.024 0l-36.955 15.309 6.886 16.628 36.955-15.306-6.886-16.631zM24 466v18h464v-18H24z\"/></svg>";
    public string SunsetIcon => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\"><path d=\"M247 27v80h18V27h-18zm-63.992 53.602l-16.631 6.886 15.309 36.955 16.628-6.886-15.306-36.955zm145.984 0l-15.306 36.955 16.628 6.886 15.309-36.955-16.63-6.886zM77.795 92.068l-12.727 12.727 56.569 56.568 12.726-12.726-56.568-56.569zm356.41 0l-56.568 56.569 12.726 12.726 56.569-56.568-12.727-12.727zM256 145.994a118.919 118.919 0 0 0-59.5 15.95c-34.215 19.754-56.177 55.048-59.129 94.056H374.63c-2.952-39.008-24.914-74.302-59.129-94.057a118.919 118.919 0 0 0-59.5-15.949zM66.488 195.377l-6.886 16.63 36.955 15.307 6.886-16.628-36.955-15.31zm379.024 0l-36.955 15.309 6.886 16.628 36.955-15.306-6.886-16.631zM24 274v18h464v-18H24zm200 62v64h-32l64 80 64-80h-32v-64h-64z\"/></svg>";

    public int SolarHistoricValueCapacity => 1;

    public DateTimeOffset FirstChargePriceTimeStamp => new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public int SpotPriceRefreshIntervalHours => 2;
    public int TokenRefreshIntervalSeconds => 59;
    public int ManualCarMinutesUntilForgetSoc => 10;
    public int RefreshableValuesRefreshIntervalSeconds => 1;
    public int SolarPowerSurplusPredictionIntervalHours => 1;

    public string QueryParamSuccess => "success";
    public string QueryParamMessage => "message";
    public string QueryParamWarning => "warning";
    public string QueryParamError => "error";
    public string QueryParamVin => "vin";
    public string QueryParamSmartCarAdded => "smartCarAdded";
    public string QueryParamTeslaConnected => "teslaConnected";
}
