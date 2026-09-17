namespace TeslaSolarCharger.Shared.Dtos.Ble;

public class DtoBleContainer
{
    /// <summary>
    /// The BLE API base URL as configured on the cars using this container.
    /// </summary>
    public string BleApiBaseUrl { get; set; } = null!;

    /// <summary>
    /// Names (or VINs) of the cars that use this BLE container.
    /// </summary>
    public List<string> CarNames { get; set; } = new();
}
