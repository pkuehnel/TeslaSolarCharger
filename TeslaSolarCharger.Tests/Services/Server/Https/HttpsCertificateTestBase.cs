using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TeslaSolarCharger.Server.Services.Https;
using TeslaSolarCharger.Server.Services.Https.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Tests.Services.Server.Https;

/// <summary>
/// Creates <see cref="HttpsCertificateService"/> instances that share a temporary certificate directory, a clock and
/// the names they cover, so a test can restart the service and move time on.
/// </summary>
public abstract class HttpsCertificateTestBase : IDisposable
{
    protected const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    protected readonly string CertificateDirectory = Path.Combine(Path.GetTempPath(), "tsc-https-tests", Guid.NewGuid().ToString("N"));
    protected DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    protected List<string> LocalNames = ["localhost", "127.0.0.1", "primary-pc", "192.168.178.93",];
    protected List<string> ConfiguredNames = [];

    protected HttpsCertificateService CreateService()
    {
        var configurationWrapper = new Mock<IConfigurationWrapper>();
        configurationWrapper.Setup(c => c.HttpsCertificateDirectory()).Returns(CertificateDirectory);
        configurationWrapper.Setup(c => c.HttpsAdditionalHostNames()).Returns(() => ConfiguredNames.ToList());
        var localNetworkNameProvider = new Mock<ILocalNetworkNameProvider>();
        localNetworkNameProvider.Setup(p => p.GetLocalHostNamesAndAddresses()).Returns(() => LocalNames.ToList());
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.DateTimeOffSetUtcNow()).Returns(() => Now);
        return new HttpsCertificateService(NullLogger<HttpsCertificateService>.Instance, configurationWrapper.Object,
            localNetworkNameProvider.Object, dateTimeProvider.Object);
    }

    protected static X509Certificate2 GetRoot(HttpsCertificateService service) =>
        X509CertificateLoader.LoadCertificate(service.GetRootCertificateDer());

    protected bool ChainsTo(X509Certificate2 server, X509Certificate2 root)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = Now.UtcDateTime;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuthenticationOid));
        return chain.Build(server);
    }

    public void Dispose()
    {
        if (Directory.Exists(CertificateDirectory))
        {
            Directory.Delete(CertificateDirectory, true);
        }
        GC.SuppressFinalize(this);
    }
}
