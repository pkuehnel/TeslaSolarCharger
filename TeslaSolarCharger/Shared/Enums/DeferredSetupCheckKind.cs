namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// A verification the user postponed. Stored outside the setup state so finishing setup does not erase it.
/// </summary>
public enum DeferredSetupCheckKind
{
    Unknown = 0,
    RealChargingTest = 1,
    CarConnectionVerification = 2,
    ChargingStationConnectionVerification = 3,
}
