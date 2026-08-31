using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Services;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface IAdapterEnumerationService
{
    /// <summary>
    /// The host's Bluetooth adapters without the worker ownership overlay (the caller merges that in, as only the
    /// worker service knows which adapters it holds).
    /// </summary>
    List<DtoBleAdapter> GetAdapters(bool bypassCache = false);

    /// <summary>
    /// Resolves a request's adapter to the canonical worker registry key and the current hciX id. Requests without
    /// an explicit adapter resolve to the container default; both paths for the same physical adapter yield the same
    /// key so they can never fight over the device.
    /// </summary>
    AdapterResolution Resolve(string? requestedStableId);
}

public class AdapterResolution
{
    /// <summary>
    /// False only for an explicitly requested adapter that is not present on this host. The default adapter always
    /// resolves; real adapter problems surface when the worker tries to open it.
    /// </summary>
    public bool Found { get; init; }
    /// <summary>
    /// Canonical registry key: the BD address when known, otherwise a stable fallback derived from the request.
    /// </summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>
    /// Current "hciX" id to pass to the worker or tesla-control; empty means "let the library pick the first
    /// adapter" (today's behavior when no BluetoothAdapter is configured and enumeration is unavailable).
    /// </summary>
    public string HciId { get; init; } = string.Empty;
    public bool IsExplicit { get; init; }
}
