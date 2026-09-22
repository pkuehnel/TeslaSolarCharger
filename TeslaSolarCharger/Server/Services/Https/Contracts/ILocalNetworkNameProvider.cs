namespace TeslaSolarCharger.Server.Services.Https.Contracts;

public interface ILocalNetworkNameProvider
{
    /// <summary>
    /// The machine's host name, "localhost" and the IP addresses of its network interfaces, normalized.
    /// </summary>
    List<string> GetLocalHostNamesAndAddresses();
}
