using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using TeslaSolarCharger.Server.Services.Https.Contracts;

namespace TeslaSolarCharger.Server.Services.Https;

public static class HttpsKestrelExtensions
{
    /// <summary>
    /// Lets every HTTPS endpoint, also one configured in ASPNETCORE_URLS, use the certificates of
    /// <see cref="IHttpsCertificateService"/>.
    /// </summary>
    public static void UseTscHttpsCertificates(this KestrelServerOptions options)
    {
        options.ConfigureHttpsDefaults(httpsOptions => httpsOptions.ServerCertificateSelector = (connectionContext, hostName) =>
            options.ApplicationServices.GetRequiredService<IHttpsCertificateService>()
                .GetServerCertificate(hostName, (connectionContext?.LocalEndPoint as IPEndPoint)?.Address));
    }
}
