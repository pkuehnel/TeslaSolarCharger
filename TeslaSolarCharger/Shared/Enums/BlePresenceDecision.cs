namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// What the age of the newest evidence about a car means. Presence is decided on this alone, never on message text.
/// </summary>
public enum BlePresenceDecision
{
    /// <summary>
    /// Nothing can be concluded: the container's scan has not been observing long enough, or it is not running at
    /// all. The last known state stays valid and no miss is recorded. Explicitly 0 so a loosely configured mock
    /// defaults to the safe no-op.
    /// </summary>
    Unknown = 0,
    /// <summary>The car was heard within the max age, by advertisement or by a command it answered.</summary>
    Present,
    /// <summary>
    /// Not heard for longer than the max age, but not long enough to call it away. The last known state stays valid
    /// and charging commands are suspended until it resolves either way.
    /// </summary>
    Uncertain,
    /// <summary>The first decision at which the car counts as away, so the caller runs the away transition once.</summary>
    JustConfirmedAway,
    /// <summary>Already away; nothing changed, so the caller writes nothing.</summary>
    AlreadyAway,
}
