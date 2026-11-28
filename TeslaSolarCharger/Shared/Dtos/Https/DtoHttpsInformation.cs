namespace TeslaSolarCharger.Shared.Dtos.Https;

public class DtoHttpsInformation
{
    /// <summary>
    /// Whether TSC listens for HTTPS at all.
    /// </summary>
    public bool IsEnabled { get; set; }
    public int? Port { get; set; }
    /// <summary>
    /// Whether the browser talks to Solar4Car directly at the address it is using right now, or through something that
    /// forwards ports, so that the HTTPS port might not be reachable.
    /// </summary>
    public HttpsReachability Reachability { get; set; }
    /// <summary>
    /// Host names and IP addresses the current certificate is valid for.
    /// </summary>
    public List<string> CoveredNames { get; set; } = new();
    public string? RootCertificateName { get; set; }
    public string? RootCertificateSha256Fingerprint { get; set; }
    public DateTimeOffset? RootCertificateValidUntil { get; set; }
}
