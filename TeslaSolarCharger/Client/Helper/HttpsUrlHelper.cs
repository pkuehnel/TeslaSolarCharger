using TeslaSolarCharger.Shared.Dtos.Https;

namespace TeslaSolarCharger.Client.Helper;

public static class HttpsUrlHelper
{
    public const string RootCertificatePath = "api/Https/RootCertificate";
    public const string HttpsInformationPath = "api/Https/GetHttpsInformation";

    public static bool IsSecure(Uri uri) => uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>
    /// The same page on the HTTPS port, so users land where they were.
    /// </summary>
    public static Uri ToHttpsUri(Uri uri, int httpsPort) =>
        new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = httpsPort, }.Uri;

    /// <summary>
    /// Whether to explain how to publish the HTTPS port: only while the page is open over HTTP through something that
    /// forwards ports, as it cannot be seen from inside whether the HTTPS port is published as well. A page loaded over
    /// HTTPS proves it is reachable.
    /// </summary>
    public static bool ShowsPortPublishingHint(DtoHttpsInformation httpsInformation, Uri currentUri) =>
        httpsInformation.IsEnabled
        && httpsInformation.Reachability == HttpsReachability.Forwarded
        && !IsSecure(currentUri);
}
