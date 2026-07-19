using Newtonsoft.Json.Linq;

namespace TeslaSolarCharger.Server.Dtos.Ble;

/// <summary>
/// Protojson output of `tesla-control state charge`. The charge state is wrapped in a VehicleData message, so the
/// root object contains a chargeState property.
/// </summary>
public class DtoBleVehicleData
{
    public DtoBleChargeState? ChargeState { get; set; }
}

public class DtoBleChargeState
{
    public int? BatteryLevel { get; set; }
    public int? ChargeLimitSoc { get; set; }
    public int? ChargerVoltage { get; set; }
    public int? ChargerActualCurrent { get; set; }
    public int? ChargerPhases { get; set; }
    public int? ChargeCurrentRequest { get; set; }
    public int? ChargerPilotCurrent { get; set; }
    public int? MinutesToFullCharge { get; set; }
    /// <summary>
    /// In the protobuf definition the charging state is a message with a oneof containing an empty message per state,
    /// so protojson serializes it as an object with a single property, e.g. {"Charging": {}}. Kept as JToken so a
    /// plain string is supported as well in case the serialization changes.
    /// </summary>
    public JToken? ChargingState { get; set; }
}
