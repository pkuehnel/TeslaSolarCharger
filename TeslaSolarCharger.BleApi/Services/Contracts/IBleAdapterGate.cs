namespace TeslaSolarCharger.BleApi.Services.Contracts;

/// <summary>
/// Serializes all access to the Bluetooth adapter. go-ble binds the HCI user channel exclusively and resets the
/// adapter while doing so, therefore only one process may use Bluetooth at a time: a second process would kill the
/// connection of the first one.
/// </summary>
public interface IBleAdapterGate
{
    /// <summary>
    /// Waits until the adapter is free. Returns false if it did not become free within the timeout.
    /// </summary>
    Task<bool> WaitAsync(TimeSpan timeout);

    /// <summary>
    /// Releases the adapter. Must only be called after a successful <see cref="WaitAsync"/>.
    /// </summary>
    void Release();
}
