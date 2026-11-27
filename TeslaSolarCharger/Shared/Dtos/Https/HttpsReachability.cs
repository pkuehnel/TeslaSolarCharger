namespace TeslaSolarCharger.Shared.Dtos.Https;

/// <summary>
/// How the browser reaches Solar4Car. Sent as a number, so the values must not change.
/// </summary>
public enum HttpsReachability
{
    /// <summary>
    /// Solar4Car cannot tell, e.g. because it does not know which address the browser used. The HTTPS address is
    /// offered, as that works in the documented setup.
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// The browser talks to Solar4Car directly, so the HTTPS port is reachable the same way.
    /// </summary>
    Reachable = 1,
    /// <summary>
    /// The browser uses an address that is not Solar4Car's own, typically because of a Docker port mapping or a
    /// reverse proxy. Whether the HTTPS port is forwarded as well cannot be seen from inside, so the HTTPS address is
    /// offered together with a hint on how to publish the port if it does not open.
    /// </summary>
    Forwarded = 2,
}
