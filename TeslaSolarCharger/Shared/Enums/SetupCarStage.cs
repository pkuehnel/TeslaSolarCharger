namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Where in a single car's journey the user is. Persisted on the draft and part of the address of the screen, so
/// returning from an account authorization or reloading the page lands on the very task that was interrupted.
/// </summary>
public enum SetupCarStage
{
    /// <summary>Which car is this: make, model, name, identification number.</summary>
    Identify = 0,

    /// <summary>How should it be controlled and where does its battery level come from.</summary>
    Connection = 1,

    /// <summary>Carrying out the chosen connection: pairing a key, authorizing an account, reaching a device.</summary>
    Connect = 2,

    /// <summary>What the car should do: how much charge to keep, when it has to be ready, what the wiring allows.</summary>
    ChargingSettings = 3,

    /// <summary>What was set up, in plain words, before anything is switched on.</summary>
    Review = 4,
}
