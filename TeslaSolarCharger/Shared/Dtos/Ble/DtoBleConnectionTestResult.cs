namespace TeslaSolarCharger.Shared.Dtos.Ble;

/// <summary>
/// Why a BLE connection test did not work. Decided on the structured command outcome and, where that is not
/// conclusive on its own, on the container's presence knowledge and a body controller probe.
/// </summary>
public enum BleConnectionTestResultType
{
    /// <summary>TSC could read the car's charge state, so everything needed for BLE control works.</summary>
    Success,
    /// <summary>The container did not hear the car, so it is either not at home or out of range of the antenna.</summary>
    CarNotFound,
    /// <summary>
    /// The car is there and answers the body controller, but its infotainment system is asleep. Nothing is broken,
    /// the car only has to be woken up.
    /// </summary>
    CarAsleep,
    /// <summary>
    /// The car is there but no secure connection can be established, which almost always means TSC's key was never
    /// added to the car.
    /// </summary>
    KeyNotPaired,
    /// <summary>The BLE container or its Bluetooth adapter could not be used, so the car was never asked at all.</summary>
    ContainerProblem,
    /// <summary>The car is there and the key works, but the request failed for another reason.</summary>
    Unknown,
}

public class DtoBleConnectionTestResult
{
    public BleConnectionTestResultType ResultType { get; set; }

    /// <summary>
    /// The underlying error text of the failed request, shown to the user as additional detail. Never parsed.
    /// </summary>
    public string? ErrorDetails { get; set; }
}
