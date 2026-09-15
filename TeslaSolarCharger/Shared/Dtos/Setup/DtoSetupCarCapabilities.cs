using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// What one particular car turned out to be able to do, as opposed to what was asked of it. Detected rather than
/// asked: a beginner cannot be expected to answer a firmware or telemetry question about their own car.
/// </summary>
public class DtoSetupCarCapabilities
{
    public int CarId { get; set; }

    /// <summary>
    /// True when Tesla told us this car's hardware cannot stream data to us. The car is then set up on the
    /// compatible fallback instead, and the user is told what that means rather than asked to choose.
    /// </summary>
    public bool IsFleetTelemetryHardwareIncompatible { get; set; }

    /// <summary>Whether the car is currently configured to stream its data.</summary>
    public bool UsesFleetTelemetry { get; set; }

    /// <summary>How far the car got with the Tesla account: no key, key registered but untested, or working.</summary>
    public TeslaCarFleetApiState? FleetApiState { get; set; }

    /// <summary>Whether this particular car is covered by a Fleet API subscription.</summary>
    public bool? IsFleetApiLicensed { get; set; }
}
