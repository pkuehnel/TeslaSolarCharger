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
    string FleetApiTokenMissingScopes { get; }
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
    string ChargeStartRequestUrl { get; }
    string ChargeStopRequestUrl { get; }
    string SetChargingAmpsRequestUrl { get; }
    string SetChargeLimitRequestUrl { get; }
    string WakeUpRequestUrl { get; }
    string VehicleRequestUrl { get; }
    string VehicleDataRequestUrl { get; }
    string TeslaTokenEncryptionKeyKey { get; }
    string FleetApiTokenUnauthorizedKey { get; }
    string FleetApiTokenExpirationTimeKey { get; }
    string FleetApiTokenStateKey { get; }
    string BackendTokenStateKey { get; }
    string IsBaseAppLicensedKey { get; }
    string IsFleetApiLicensedKey { get; }
    int FleetTelemetryReconfigurationBufferHours { get; }
    string HomeDetectionViaConvertedKey { get; }
    int WeatherDateRefreshIntervallHours { get; }
    string MeterValueEstimatesCreated { get; }
    int MeterValueDatabaseSaveIntervalMinutes { get; }
    int WeatherPredictionInFutureDays { get; }
    string OcppChargePointConnectorIdDelimiter { get; }
    string DefaultIdTag { get; }
    int HomeBatteryMinSocRefreshIntervalMinutes { get; }
    string PrimaryColor { get; }
    string SecondaryColor { get; }
    string SunriseIcon { get; }
    string SunsetIcon { get; }
    string UnknownCarName { get; }
    string SolarPowerColor { get; }
    string ConsumptionColor { get; }
    string FeedInColor { get; }
    string GridColor { get; }
    string BatteryColor { get; }
    string EvChargingColor { get; }
    string HomeConsumptionColor { get; }
    string SolarPowerPredictionColor { get; }
    string HomeConsumptionPredictionColor { get; }
    string BatterySocColor { get; }
    string HomeConsumptionChartColor { get; }
    string GridExportColor { get; }
    string GridImportColor { get; }
    string BatteryChargingColor { get; }
    string BatteryDischargingColor { get; }
    string SolarPowerIcon { get; }
    string HomeBatteryIcon { get; }
    string HomePowerIcon { get; }
    string EvPowerIcon { get; }
    int CarCapabilityMaxCurrentAboveMeasuredCurrent { get; }
    string TeslasAddedToAllowedCars { get; }
}
