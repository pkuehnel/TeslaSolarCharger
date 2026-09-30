using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Dtos.Ble;

/// <summary>
/// The cars configured with the same container and adapter, together with the one presence answer fetched for them in
/// this refresh cycle. Exactly one of <see cref="Presence"/> and <see cref="PresenceException"/> is set.
/// </summary>
public class DtoBleGroupPresence
{
    /// <summary>Normalized container URL as configured on the cars.</summary>
    public string? Host { get; init; }

    /// <summary>Normalized adapter address as configured on the cars, null when none is selected.</summary>
    public string? Adapter { get; init; }

    public List<DtoCar> Cars { get; init; } = new();

    public DtoBlePresenceResult? Presence { get; init; }

    public Exception? PresenceException { get; init; }
}
