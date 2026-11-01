namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IPilotCurrentProbeService
{
    /// <summary>
    /// Identifies the Tesla plugged into each managed OCPP connector that has no car yet: the connector briefly offers
    /// a current no candidate car currently reports as pilot current, and the one car whose pilot current switches to
    /// it is the car plugged into that connector. Probes every connector at most once per plug in of the connector or
    /// of one of its candidate cars.
    /// </summary>
    Task ProbeUnassignedConnectors(CancellationToken cancellationToken);
}
