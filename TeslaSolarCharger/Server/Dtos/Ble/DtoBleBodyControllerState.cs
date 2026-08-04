namespace TeslaSolarCharger.Server.Dtos.Ble;

/// <summary>
/// Protojson output of `tesla-control body-controller-state` (VCSEC domain, works while the car is asleep).
/// </summary>
public class DtoBleBodyControllerState
{
    /// <summary>
    /// VEHICLE_SLEEP_STATUS_AWAKE, VEHICLE_SLEEP_STATUS_ASLEEP or VEHICLE_SLEEP_STATUS_UNKNOWN
    /// </summary>
    public string? VehicleSleepStatus { get; set; }
    public string? VehicleLockState { get; set; }
    /// <summary>
    /// VEHICLE_USER_PRESENCE_PRESENT, VEHICLE_USER_PRESENCE_NOT_PRESENT or VEHICLE_USER_PRESENCE_UNKNOWN. Null means
    /// VEHICLE_USER_PRESENCE_UNKNOWN, which is the proto3 default and therefore never serialized (see
    /// <see cref="DtoBleClosureStatuses"/>).
    /// </summary>
    public string? UserPresence { get; set; }
    /// <summary>
    /// Door, trunk, frunk, charge port and tonneau states. Null when every closure is closed, and also while the car
    /// is asleep.
    /// </summary>
    public DtoBleClosureStatuses? ClosureStatuses { get; set; }
}

/// <summary>
/// Closure states from the VCSEC body controller state. Each value is a CLOSURESTATE_* string, e.g. CLOSURESTATE_OPEN
/// or CLOSURESTATE_AJAR.
/// </summary>
/// <remarks>
/// A null property means CLOSURESTATE_CLOSED. The BLE container marshals the VCSEC answer with protojson's default
/// options, which omit every field holding its proto3 default value, and CLOSURESTATE_CLOSED is 0 in Tesla's proto.
/// The string "CLOSURESTATE_CLOSED" is consequently never sent, so never test for it without also accepting null.
/// </remarks>
public class DtoBleClosureStatuses
{
    public string? FrontDriverDoor { get; set; }
    public string? FrontPassengerDoor { get; set; }
    public string? RearDriverDoor { get; set; }
    public string? RearPassengerDoor { get; set; }
    public string? FrontTrunk { get; set; }
    public string? RearTrunk { get; set; }
    public string? ChargePort { get; set; }
    public string? Tonneau { get; set; }
}
