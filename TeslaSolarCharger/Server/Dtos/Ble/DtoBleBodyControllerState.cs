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
    /// VEHICLE_USER_PRESENCE_PRESENT, VEHICLE_USER_PRESENCE_NOT_PRESENT or VEHICLE_USER_PRESENCE_UNKNOWN. Only present
    /// while the car is awake.
    /// </summary>
    public string? UserPresence { get; set; }
    /// <summary>
    /// Door, trunk, frunk, charge port and tonneau states. Only present while the car is awake (absent while asleep).
    /// </summary>
    public DtoBleClosureStatuses? ClosureStatuses { get; set; }
}

/// <summary>
/// Closure states from the VCSEC body controller state. Each value is a CLOSURESTATE_* string, e.g.
/// CLOSURESTATE_CLOSED or CLOSURESTATE_OPEN (verified against a real BLE container).
/// </summary>
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
