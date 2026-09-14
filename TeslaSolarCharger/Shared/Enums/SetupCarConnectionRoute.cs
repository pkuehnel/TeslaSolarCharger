namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// How a single car is controlled and where its data comes from. Chosen per car instead of per household, so a
/// mixed installation (e.g. one Tesla on Bluetooth, one on the cloud API) can be described.
/// </summary>
public enum SetupCarConnectionRoute
{
    /// <summary>The user has not chosen a route yet. Distinct from any concrete route, never assumed to be one.</summary>
    Undecided = 0,
    /// <summary>Tesla controlled via a local Bluetooth container. No per-car Fleet API license needed.</summary>
    TeslaBluetooth = 1,
    /// <summary>Tesla controlled via Tesla's Fleet API. Requires a per-car license.</summary>
    TeslaCloud = 2,
    /// <summary>Non-Tesla controlled by an OCPP charger, vehicle data supplied by SmartCar.</summary>
    SmartCarWithChargingStation = 3,
    /// <summary>Non-Tesla controlled by an OCPP charger with manually entered or estimated battery data.</summary>
    ChargingStationOnly = 4,
}
