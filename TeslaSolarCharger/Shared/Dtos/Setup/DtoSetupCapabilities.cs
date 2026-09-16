using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// What the installation can currently do, detected rather than asked: which accounts are connected, which
/// measurements arrive, and which charging station connectors have reported in. Feeding the decision service from
/// one detected snapshot keeps its rules free of service lookups and makes them testable.
/// </summary>
public class DtoSetupCapabilities
{
    /// <summary>Null when the state could not be determined, which is not the same as "not connected".</summary>
    public TokenState? BackendTokenState { get; set; }

    /// <summary>
    /// Null when the licence could not be checked at all, which is deliberately different from "not licensed": a
    /// failed lookup must not be reported to the user as a missing licence.
    /// </summary>
    public bool? IsBaseAppLicensed { get; set; }

    /// <summary>Null when the state could not be determined, which is not the same as "not connected".</summary>
    public TokenState? FleetApiTokenState { get; set; }

    public bool HasGridPowerSource { get; set; }
    public bool HasSolarGenerationSource { get; set; }
    public bool HasHomeBatterySocSource { get; set; }
    public bool HasHomeBatteryPowerSource { get; set; }

    /// <summary>Connectors of charging stations that have connected over OCPP at least once.</summary>
    public List<int> KnownChargingStationConnectorIds { get; set; } = new();

    /// <summary>
    /// True when TeslaMate supplies the vehicle data. Cars then cannot stream their data to us as well, which the
    /// decision service reports as an incompatibility instead of silently picking one.
    /// </summary>
    public bool UsesTeslaMateAsDataSource { get; set; }
}
