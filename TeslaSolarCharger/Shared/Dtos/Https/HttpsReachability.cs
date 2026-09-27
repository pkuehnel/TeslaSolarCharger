namespace TeslaSolarCharger.Shared.Dtos.Https;

public enum HttpsReachability
{
    /// <summary>
    /// TSC cannot tell, e.g. because it does not know which address the browser used. The HTTPS address is offered,
    /// as that works in the documented setup.
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// The browser talks to TSC directly, so the HTTPS port is reachable the same way.
    /// </summary>
    Reachable = 1,
    /// <summary>
    /// Something forwards the HTTP port to TSC, typically a Docker port mapping, and the HTTPS port is not published
    /// alongside it.
    /// </summary>
    PortNotPublished = 2,
}
