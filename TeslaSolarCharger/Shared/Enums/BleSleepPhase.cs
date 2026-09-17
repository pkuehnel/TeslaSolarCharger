namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Phase of the BLE sleep window state machine for a car, used for display on the home page.
/// </summary>
public enum BleSleepPhase
{
    /// <summary>The car is awake and TSC is waiting for it to stay unchanged and closed up before starting a window.</summary>
    WaitingToSleep,

    /// <summary>The car is awake and inside a sleep window: the infotainment poll is withheld so it can fall asleep.</summary>
    TryingToSleep,

    /// <summary>The car has fallen asleep.</summary>
    Asleep,
}
