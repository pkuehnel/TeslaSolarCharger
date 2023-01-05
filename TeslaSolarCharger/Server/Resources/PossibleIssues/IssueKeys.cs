namespace TeslaSolarCharger.Server.Resources.PossibleIssues;

public class IssueKeys
{
    public string MqttNotConnected = "MqttNotConnected";
    public string CarSocLimitNotReadable = "CarSocLimitNotReadable";
    public string CarSocNotReadable = "CarSocNotReadable";
    public string GridPowerNotAvailable = "GridPowerNotAvailable";
    public string InverterPowerNotAvailable = "InverterPowerNotAvailable";
    public string HomeBatterySocNotAvailable = "HomeBatterySocNotAvailable";
    public string HomeBatterySocNotPlausible = "HomeBatterySocNotPlausible";
    public string HomeBatteryPowerNotAvailable = "HomeBatteryPowerNotAvailable";
    public string HomeBatteryHalfConfigured = "HomeBatteryHalfConfigured";
    public string HomeBatteryMinimumSocNotConfigured = "HomeBatteryMinimumSocNotConfigured";
    public string HomeBatteryChargingPowerNotConfigured = "HomeBatteryChargingPowerNotConfigured";
    public string TeslaMateApiNotAvailable = "TeslaMateApiNotAvailable";
    public string DatabaseNotAvailable = "DatabaseNotAvailable";
    public string GeofenceNotAvailable = "GeofenceNotAvailable";
    public string CarIdNotAvailable = "CarIdNotAvailable";
    public string VersionNotUpToDate = "VersionNotUpToDate";
    public string CorrectionFactorZero = "CorrectionFactorZero";
}
