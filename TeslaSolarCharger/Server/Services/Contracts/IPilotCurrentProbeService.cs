namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IPilotCurrentProbeService
{
    /// <summary>
    /// Identifies the Tesla plugged into each managed OCPP connector whose car is not confirmed by the user or a probe
    /// (no car yet, or a car matching only guessed, e.g. the only other allowed car): the connector briefly offers
    /// a current no candidate car currently reports as pilot current, and the one car whose pilot current switches to
    /// it is the car plugged into that connector. Probes every connector at most once per plug in of the connector or
    /// of one of its candidate cars, plus once more if a candidate car comes online within a few minutes after the
    /// probe. A probe that would exceed the max combined current is postponed.
    /// </summary>
    /// <returns>The current the other load points have to leave free so a postponed probe fits in a later cycle.</returns>
    Task<int> ProbeUnconfirmedConnectors(CancellationToken cancellationToken);
}
