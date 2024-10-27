namespace TeslaSolarCharger.Shared.Enums;

public enum CarValueType
{
    ModuleTempMin,
    ModuleTempMax,
    BatteryHeaterOn,
    ChargeState,
    ChargeAmps,
    ChargeCurrentRequest,
    ChargePortLatch,
    DetailedChargeState,
    IsPluggedIn,
    IsCharging,
    ChargerPilotCurrent,
    Location,
    Longitude,
    Latitude,
    Unknown = 9999,
}
