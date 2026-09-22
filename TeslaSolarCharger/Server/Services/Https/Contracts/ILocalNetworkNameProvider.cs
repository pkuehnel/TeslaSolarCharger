namespace TeslaSolarCharger.Server.Services.Https.Contracts;

public interface ILocalNetworkNameProvider
{
    /// <summary>
    /// The machine's host name, "localhost" and the IP addresses of its network interfaces, normalized.
    /// </summary>
    List<string> GetLocalHostNamesAndAddresses();

    /// <summary>
    /// The IP addresses a host name resolves to, to tell whether a browser reaches this machine directly.
    /// </summary>
    /// <returns>Empty if the name cannot be resolved</returns>
    List<string> ResolveAddresses(string hostName);
}
