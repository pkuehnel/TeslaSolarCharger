namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Result of registering an out of BLE range poll result for a car.
/// </summary>
public enum BleAwayConfirmation
{
    //Explicitly 0 so loosely configured mocks default to the safe no-op.
    NotConfirmed = 0,
    JustConfirmed,
    AlreadyConfirmed,
}
