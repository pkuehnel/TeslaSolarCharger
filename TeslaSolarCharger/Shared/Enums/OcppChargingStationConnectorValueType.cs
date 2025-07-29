namespace TeslaSolarCharger.Shared.Enums;

public enum OcppChargingStationConnectorValueType
{
    //Use explicit numbers to match CarValueType
    ChargeAmps = 4,//4
    IsPluggedIn = 8,//8
    IsCharging = 9,//9
    ChargerVoltage = 17,//17
    Unknown = 9999,
}
