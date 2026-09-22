using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using TeslaSolarCharger.Server.Services.Https.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Https;
using TeslaSolarCharger.Shared.Helper;

namespace TeslaSolarCharger.Server.Services.Https;

/// <summary>
/// Issues TSC's HTTPS certificates. Each installation creates its own root certificate once, which users install on
/// their devices, so the server certificate it signs is trusted. The server certificate covers every host name and IP
/// address TSC is opened with and is reissued as soon as a browser asks for one it does not cover yet.
/// </summary>
public partial class HttpsCertificateService(
    ILogger<HttpsCertificateService> logger,
    IConfigurationWrapper configurationWrapper,
    ILocalNetworkNameProvider localNetworkNameProvider,
    IDateTimeProvider dateTimeProvider) : IHttpsCertificateService
{
    /// <summary>
    /// Caps the certificate's names, so random names in handshakes cannot make it grow endlessly.
    /// </summary>
    public const int MaxCoveredNames = 50;
    public static readonly TimeSpan RootValidity = TimeSpan.FromDays(3650);
    //Apple rejects server certificates that are valid for longer than 398 days
    public static readonly TimeSpan ServerValidity = TimeSpan.FromDays(397);
    public static readonly TimeSpan RenewalLeadTime = TimeSpan.FromDays(30);
    //A Raspberry Pi without a real time clock starts in 1970 until it synced its clock. A root created before would
    //expire right away and every user would have to install a new one.
    public static readonly DateTimeOffset EarliestPlausibleTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan LocalNamesRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BackdateBy = TimeSpan.FromDays(1);
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private const string FallbackCommonName = "Solar4Car";
    public const string RootCertificateFileName = "root.crt.pem";
    public const string RootKeyFileName = "root.key.pem";
    public const string ServerCertificateFileName = "server.crt.pem";
    public const string ServerKeyFileName = "server.key.pem";
    public const string RequestedNamesFileName = "requested-names.json";

    private readonly Lock _lock = new();
    private X509Certificate2? _root;
    private X509Certificate2? _server;
    private string? _serverRootThumbprint;
    private HashSet<string> _serverNames = new(StringComparer.Ordinal);
    private List<string>? _requestedNames;
    private List<string> _localNames = [];
    private DateTimeOffset? _localNamesReadAt;

    public X509Certificate2 GetServerCertificate(string? requestedHostName, IPAddress? localAddress)
    {
        var requestedName = GetRequestedName(requestedHostName, localAddress);
        lock (_lock)
        {
            return EnsureServerCertificate(requestedName);
        }
    }

    public byte[] GetRootCertificateDer()
    {
        lock (_lock)
        {
            return EnsureRoot(GetCurrentTime()).Export(X509ContentType.Cert);
        }
    }

    public DtoHttpsInformation GetHttpsInformation(IEnumerable<string> serverAddresses)
    {
        var port = GetHttpsPort(serverAddresses);
        lock (_lock)
        {
            EnsureServerCertificate(null);
            var root = _root!;
            return new DtoHttpsInformation
            {
                IsEnabled = port != default,
                Port = port,
                CoveredNames = _serverNames.Order(StringComparer.Ordinal).ToList(),
                RootCertificateName = root.GetNameInfo(X509NameType.SimpleName, false),
                RootCertificateSha256Fingerprint = string.Join(":", SHA256.HashData(root.RawData).Select(b => b.ToString("X2"))),
                RootCertificateValidUntil = ValidUntil(root),
            };
        }
    }

    /// <summary>
    /// The port of the first HTTPS address, e.g. 7191 of "https://[::]:7191".
    /// </summary>
    public static int? GetHttpsPort(IEnumerable<string> serverAddresses)
    {
        foreach (var address in serverAddresses)
        {
            if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var match = PortRegex().Match(address);
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
        }
        return default;
    }

    private static string? GetRequestedName(string? requestedHostName, IPAddress? localAddress)
    {
        if (!string.IsNullOrWhiteSpace(requestedHostName))
        {
            return HttpsHostNameHelper.Normalize(requestedHostName);
        }
        return localAddress == default ? default : HttpsHostNameHelper.NormalizeIpAddress(localAddress).ToString();
    }

    private X509Certificate2 EnsureServerCertificate(string? requestedName)
    {
        var now = GetCurrentTime();
        var root = EnsureRoot(now);
        if (_server == default)
        {
            _server = TryLoadServerCertificate(root, now);
        }
        if (requestedName != default && !_serverNames.Contains(requestedName))
        {
            TryAddRequestedName(requestedName, now);
        }
        var requiredNames = GetRequiredNames(now);
        if (_server == default
            || _serverRootThumbprint != root.Thumbprint
            || !requiredNames.All(_serverNames.Contains)
            || NeedsRenewal(_server, now))
        {
            _server = IssueServerCertificate(root, requiredNames, now);
        }
        return _server;
    }

    private DateTimeOffset GetCurrentTime()
    {
        var now = dateTimeProvider.DateTimeOffSetUtcNow();
        if (now < EarliestPlausibleTime)
        {
            throw new InvalidOperationException($"The system clock shows {now:u}, so no HTTPS certificate is created before it is set.");
        }
        return now;
    }

    private X509Certificate2 EnsureRoot(DateTimeOffset now)
    {
        if (_root != default && !NeedsRenewal(_root, now))
        {
            return _root;
        }
        _root = TryLoadRoot(now) ?? CreateRoot(now);
        return _root;
    }

    private X509Certificate2? TryLoadRoot(DateTimeOffset now)
    {
        var root = TryLoadCertificate(RootCertificateFileName, RootKeyFileName);
        if (root == default)
        {
            return default;
        }
        if (NeedsRenewal(root, now))
        {
            logger.LogWarning("The HTTPS root certificate expires on {validUntil}, so a new one is created. Devices need the new one installed.", ValidUntil(root));
            return default;
        }
        return root;
    }

    private X509Certificate2 CreateRoot(DateTimeOffset now)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var subject = new X500DistinguishedNameBuilder();
        //The creation time tells apart the roots of several installations or of a recreated one on the user's device
        subject.AddCommonName($"Solar4Car local HTTPS root {now:yyyy-MM-dd HH:mm}");
        subject.AddOrganizationName("Solar4Car");
        var request = new CertificateRequest(subject.Build(), key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var root = request.CreateSelfSigned(now - BackdateBy, now + RootValidity);
        WriteCertificateFiles(RootCertificateFileName, root, RootKeyFileName, key);
        logger.LogInformation("Created the HTTPS root certificate {name}, valid until {validUntil}", root.Subject, ValidUntil(root));
        return LoadWithUsableKey(root);
    }

    private X509Certificate2? TryLoadServerCertificate(X509Certificate2 root, DateTimeOffset now)
    {
        var server = TryLoadCertificate(ServerCertificateFileName, ServerKeyFileName);
        if (server == default)
        {
            return default;
        }
        if (!IsIssuedBy(server, root, now))
        {
            logger.LogInformation("The stored HTTPS server certificate does not belong to the current root certificate, so a new one is issued.");
            return default;
        }
        _serverNames = ReadCoveredNames(server);
        _serverRootThumbprint = root.Thumbprint;
        return server;
    }

    private X509Certificate2 IssueServerCertificate(X509Certificate2 root, List<string> names, DateTimeOffset now)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName(names.FirstOrDefault(name => !HttpsHostNameHelper.TryParseIpAddress(name, out _)) ?? FallbackCommonName);
        var request = new CertificateRequest(subject.Build(), key, HashAlgorithmName.SHA256);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        foreach (var name in names)
        {
            if (HttpsHostNameHelper.TryParseIpAddress(name, out var ipAddress))
            {
                subjectAlternativeNames.AddIpAddress(ipAddress);
            }
            else
            {
                subjectAlternativeNames.AddDnsName(name);
            }
        }
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ServerAuthenticationOid),], false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(root, true, false));
        //A certificate must lie within its issuer's validity, even if the clock went back since the root was created
        var notBefore = Max(now - BackdateBy, new DateTimeOffset(root.NotBefore.ToUniversalTime(), TimeSpan.Zero));
        var notAfter = Min(now + ServerValidity, ValidUntil(root));
        using var issued = request.Create(root, notBefore, notAfter, CreateSerialNumber());
        using var server = issued.CopyWithPrivateKey(key);
        WriteCertificateFiles(ServerCertificateFileName, server, ServerKeyFileName, key);
        _serverNames = names.ToHashSet(StringComparer.Ordinal);
        _serverRootThumbprint = root.Thumbprint;
        logger.LogInformation("Issued the HTTPS server certificate for {names}, valid until {validUntil}", string.Join(", ", names), notAfter);
        return LoadWithUsableKey(server);
    }

    private List<string> GetRequiredNames(DateTimeOffset now)
    {
        return GetLocalNames(now)
            .Concat(GetConfiguredNames())
            .Concat(GetRequestedNames())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxCoveredNames)
            .ToList();
    }

    private List<string> GetLocalNames(DateTimeOffset now)
    {
        if (_localNamesReadAt == default || now - _localNamesReadAt > LocalNamesRefreshInterval)
        {
            _localNames = localNetworkNameProvider.GetLocalHostNamesAndAddresses();
            _localNamesReadAt = now;
        }
        return _localNames;
    }

    private List<string> GetConfiguredNames()
    {
        try
        {
            return configurationWrapper.HttpsAdditionalHostNames();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not read the additional host names for the HTTPS certificate");
            return [];
        }
    }

    private List<string> GetRequestedNames()
    {
        if (_requestedNames != default)
        {
            return _requestedNames;
        }
        _requestedNames = [];
        var path = GetPath(RequestedNamesFileName);
        if (!File.Exists(path))
        {
            return _requestedNames;
        }
        try
        {
            _requestedNames = (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [])
                .Where(HttpsHostNameHelper.IsValidHostName)
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "Could not read the host names browsers requested, starting without them");
        }
        return _requestedNames;
    }

    private void TryAddRequestedName(string requestedName, DateTimeOffset now)
    {
        if (!HttpsHostNameHelper.IsValidHostName(requestedName))
        {
            logger.LogDebug("Not adding {requestedName} to the HTTPS certificate, as it is no valid host name", requestedName);
            return;
        }
        var requestedNames = GetRequestedNames();
        if (requestedNames.Contains(requestedName))
        {
            return;
        }
        if (GetRequiredNames(now).Count >= MaxCoveredNames)
        {
            logger.LogWarning("Not adding {requestedName} to the HTTPS certificate, as it already covers {maxCoveredNames} names", requestedName, MaxCoveredNames);
            return;
        }
        requestedNames.Add(requestedName);
        EnsureDirectory();
        File.WriteAllText(GetPath(RequestedNamesFileName), JsonSerializer.Serialize(requestedNames));
        logger.LogInformation("TSC was opened as {requestedName}, adding it to the HTTPS certificate", requestedName);
    }

    private X509Certificate2? TryLoadCertificate(string certificateFileName, string keyFileName)
    {
        var certificatePath = GetPath(certificateFileName);
        var keyPath = GetPath(keyFileName);
        if (!File.Exists(certificatePath) || !File.Exists(keyPath))
        {
            return default;
        }
        try
        {
            using var certificate = X509Certificate2.CreateFromPemFile(certificatePath, keyPath);
            return LoadWithUsableKey(certificate);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or ArgumentException)
        {
            logger.LogWarning(ex, "Could not load the HTTPS certificate {certificatePath}, creating a new one", certificatePath);
            return default;
        }
    }

    private void WriteCertificateFiles(string certificateFileName, X509Certificate2 certificate, string keyFileName, ECDsa key)
    {
        EnsureDirectory();
        var keyPath = GetPath(keyFileName);
        File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem());
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        File.WriteAllText(GetPath(certificateFileName), certificate.ExportCertificatePem());
    }

    private void EnsureDirectory() => Directory.CreateDirectory(configurationWrapper.HttpsCertificateDirectory());

    private string GetPath(string fileName) => Path.Combine(configurationWrapper.HttpsCertificateDirectory(), fileName);

    /// <summary>
    /// SslStream on Windows cannot use the in-memory key of a certificate created or read from PEM, so the
    /// certificate goes through PKCS#12 once. Elsewhere this changes nothing.
    /// </summary>
    private static X509Certificate2 LoadWithUsableKey(X509Certificate2 certificate) =>
        X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);

    private static bool IsIssuedBy(X509Certificate2 server, X509Certificate2 root, DateTimeOffset now)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = now.UtcDateTime;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuthenticationOid));
        return chain.Build(server);
    }

    public static HashSet<string> ReadCoveredNames(X509Certificate2 certificate)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var extension = certificate.Extensions.FirstOrDefault(e => e.Oid?.Value == SubjectAlternativeNameOid);
        if (extension == default)
        {
            return names;
        }
        var subjectAlternativeName = new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical);
        names.UnionWith(subjectAlternativeName.EnumerateDnsNames());
        names.UnionWith(subjectAlternativeName.EnumerateIPAddresses().Select(ip => HttpsHostNameHelper.NormalizeIpAddress(ip).ToString()));
        return names;
    }

    private static bool NeedsRenewal(X509Certificate2 certificate, DateTimeOffset now) => ValidUntil(certificate) - RenewalLeadTime < now;

    private static DateTimeOffset ValidUntil(X509Certificate2 certificate) => new(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero);

    private static byte[] CreateSerialNumber()
    {
        var serialNumber = RandomNumberGenerator.GetBytes(16);
        //Positive and without leading zero byte, as DER requires
        serialNumber[0] = (byte)((serialNumber[0] & 0x7F) | 0x40);
        return serialNumber;
    }

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;

    [GeneratedRegex(@":(\d+)/?$")]
    private static partial Regex PortRegex();
}
