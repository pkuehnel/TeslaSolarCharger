namespace TeslaSolarCharger.Shared.Enums;

public enum CarValueType
{
    ModuleTempMin,//0
    ModuleTempMax,//1
    BatteryHeaterOn,//2
    ChargeState,//3
    ChargeAmps,//4
    ChargeCurrentRequest,//5
    ChargePortLatch,//6
    DetailedChargeState,//7
    IsPluggedIn,//8
    IsCharging,//9
    ChargerPilotCurrent,//10
    Location,//11
    Longitude,//12
    Latitude,//13
    StateOfCharge,//14
    StateOfChargeLimit,//15
    ChargerPhases,//16
    ChargerVoltage,//17
    AsleepOrOffline,//18
    Gear,//19
    Speed,//20
    VehicleName,//21
    LocatedAtHome,//22
    LocatedAtWork,//23
    LocatedAtFavorite,//24
    Unknown = 9999,
}
