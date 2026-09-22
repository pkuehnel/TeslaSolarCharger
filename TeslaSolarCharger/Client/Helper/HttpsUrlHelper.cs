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
}
