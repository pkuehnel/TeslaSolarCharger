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
    public string? UserPresence { get; set; }
}
