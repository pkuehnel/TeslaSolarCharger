using System.Net;
using System.Security.Cryptography.X509Certificates;
using TeslaSolarCharger.Shared.Dtos.Https;

namespace TeslaSolarCharger.Server.Services.Https.Contracts;

public interface IHttpsCertificateService
{
    /// <summary>
    /// The server certificate for a TLS handshake. It also covers <paramref name="requestedHostName"/> (the name the
    /// browser asked for) or, as browsers send no name when opened by IP address, <paramref name="localAddress"/> (the
    /// address the connection arrived on), and is reissued when it did not yet.
    /// </summary>
    X509Certificate2 GetServerCertificate(string? requestedHostName, IPAddress? localAddress);

    /// <summary>
    /// The root certificate users install on their devices, DER encoded and without private key.
    /// </summary>
    byte[] GetRootCertificateDer();

    /// <param name="serverAddresses">The addresses Kestrel listens on, the HTTPS port is read from them.</param>
    DtoHttpsInformation GetHttpsInformation(IEnumerable<string> serverAddresses);
}
