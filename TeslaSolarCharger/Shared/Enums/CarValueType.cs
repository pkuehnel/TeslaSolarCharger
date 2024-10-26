﻿namespace TeslaSolarCharger.Shared.Enums;

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
    Unknown = 9999,
}
