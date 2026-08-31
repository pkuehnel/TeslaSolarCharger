using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// The intake answers for the merged "Cars &amp; charging" setup step. They drive the tailored setup plan that is
/// shown to the user and are persisted in the <see cref="DtoSetupCache"/> so they survive a page reload.
/// </summary>
public class DtoCarsChargingSetupAnswers
{
    public int? ElectricCarCount { get; set; }
    public int? TeslaCount { get; set; }
    public SetupOcppStationAnswer? HasOcppChargingStation { get; set; }

    /// <summary>
    /// Whether the user can place a Bluetooth device (e.g. a Raspberry Pi running TeslaSolarCharger) within a few
    /// metres of where the Tesla parks. true =&gt; recommend free BLE control, false/null =&gt; recommend the paid
    /// Fleet API. Only relevant when <see cref="TeslaCount"/> &gt; 0.
    /// </summary>
    public bool? CanPlaceBleDeviceNearTesla { get; set; }
}
