namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Identifies one step of applying a setup state to the real configuration. Recorded per operation so a retry
/// after a partial failure skips what already succeeded instead of applying it twice.
/// </summary>
public enum SetupOperationKey
{
    Unknown = 0,
    SaveBaseConfiguration = 1,
    SaveChargePrice = 2,
    SaveCarDraft = 3,
    ActivateCar = 4,
    ActivateChargingStationConnector = 5,
    CompleteSetup = 6,
}
