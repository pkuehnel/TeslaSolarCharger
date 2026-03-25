namespace TeslaSolarCharger.Shared.SignalRClients;

public class DataTypeConstants
{
    public const string PvValues = "PvValues";
    public const string LoadPointOverviewValues = "LoadPointOverviewValues";
    public const string CarOverviewState = "CarOverviewState";
    public const string ChargingConnectorOverviewState = "ChargingConnectorOverviewState";

    public const string LoadPointMatchesChangeTrigger = "LoadPointMatchesChangeTrigger";
    public const string NotChargingAsExpectedChangeTrigger = "NotChargingAsExpectedChangeTrigger";
    public const string ChargingSchedulesChangeTrigger = "ChargingSchedulesChangeTrigger";
    public const string EnergyPredictionChangeTrigger = "EnergyPredictionChangeTrigger";
    public const string DynamicHomeBatteryMinSocChanged = "DynamicHomeBatteryMinSocChanged";
}
