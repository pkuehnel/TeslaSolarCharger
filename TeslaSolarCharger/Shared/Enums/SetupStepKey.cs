namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Stable identifier of a setup assistant step. The values are persisted in the setup state, so they must never
/// change: a removed step keeps its value reserved and a new step gets a new value. Using keys instead of the
/// stepper's positional index means inserting or reordering a step no longer sends a returning user to a
/// different screen than the one they left.
/// </summary>
public enum SetupStepKey
{
    Unknown = 0,
    Welcome = 1,
    CloudConnection = 2,
    SolarAndBattery = 3,
    Location = 4,
    Prices = 5,
    CarsAndCharging = 6,
    Finish = 7,
}
