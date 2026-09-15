namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IDeferredChargingTestObserver
{
    /// <summary>
    /// Looks at what is charging right now and ticks off any postponed charging test whose equipment is actually
    /// charging at home. The test resolves itself this way rather than by sending commands: the proof that setup
    /// worked is the car charging, not a command that was accepted.
    /// </summary>
    Task ObserveRunningCharges(CancellationToken cancellationToken);
}
