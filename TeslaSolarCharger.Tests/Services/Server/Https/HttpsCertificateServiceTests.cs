using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using TeslaSolarCharger.Server.Services.Https;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Https;

public class HttpsCertificateServiceTests : HttpsCertificateTestBase
{
    [Fact]
    public void GetServerCertificate_CoversTheLocalAndTheConfiguredNames()
    {
        ConfiguredNames = ["tsc.example.lan", "10.1.2.3",];
        var service = CreateService();

        var server = service.GetServerCertificate(null, null);

        var coveredNames = HttpsCertificateService.ReadCoveredNames(server);
        Assert.Superset(LocalNames.Concat(ConfiguredNames).ToHashSet(), coveredNames);
        Assert.True(server.MatchesHostname("tsc.example.lan"));
        Assert.True(server.MatchesHostname("192.168.178.93"));
    }

    [Fact]
    public void GetServerCertificate_IsAServerCertificateSignedByTheRoot()
    {
        var service = CreateService();

        var server = service.GetServerCertificate(null, null);

        Assert.True(server.HasPrivateKey);
        Assert.True(ChainsTo(server, GetRoot(service)));
        Assert.False(server.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);
        Assert.Contains(server.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>(),
            oid => oid.Value == ServerAuthenticationOid);
    }

    [Fact]
    public void GetServerCertificate_IsValidFor397Days()
    {
        var server = CreateService().GetServerCertificate(null, null);

        Assert.Equal((Now + HttpsCertificateService.ServerValidity).UtcDateTime, server.NotAfter.ToUniversalTime(), TimeSpan.FromSeconds(1));
        Assert.True(server.NotBefore.ToUniversalTime() <= Now.UtcDateTime);
    }

    [Fact]
    public void GetRootCertificateDer_IsACertificateAuthorityWithoutPrivateKey()
    {
        var root = GetRoot(CreateService());

        Assert.False(root.HasPrivateKey);
        Assert.True(root.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);
        Assert.Equal(root.Subject, root.Issuer);
        Assert.Equal((Now + HttpsCertificateService.RootValidity).UtcDateTime, root.NotAfter.ToUniversalTime(), TimeSpan.FromSeconds(1));
        Assert.Contains("Solar4Car local HTTPS root", root.Subject);
    }

    [Fact]
    public void GetServerCertificate_AddsARequestedHostName()
    {
        var service = CreateService();

        var server = service.GetServerCertificate("TSC.Home.Arpa.", null);

        Assert.True(server.MatchesHostname("tsc.home.arpa"));
        Assert.True(ChainsTo(server, GetRoot(service)));
    }

    [Fact]
    public void GetServerCertificate_AddsTheLocalAddressWhenNoHostNameIsRequested()
    {
        //Browsers send no host name when opened by IP address, e.g. behind a Docker port mapping
        var server = CreateService().GetServerCertificate(null, IPAddress.Parse("::ffff:10.0.0.5"));

        Assert.Contains("10.0.0.5", HttpsCertificateService.ReadCoveredNames(server));
    }

    [Fact]
    public void GetServerCertificate_PrefersTheRequestedHostNameOverTheLocalAddress()
    {
        var server = CreateService().GetServerCertificate("tsc.lan", IPAddress.Parse("10.0.0.5"));

        var coveredNames = HttpsCertificateService.ReadCoveredNames(server);
        Assert.Contains("tsc.lan", coveredNames);
        Assert.DoesNotContain("10.0.0.5", coveredNames);
    }

    [Fact]
    public void GetServerCertificate_IgnoresAnInvalidRequestedName()
    {
        var service = CreateService();
        var before = service.GetServerCertificate(null, null);

        var after = service.GetServerCertificate("bad_name!", null);

        Assert.Equal(before.Thumbprint, after.Thumbprint);
        Assert.DoesNotContain("bad_name!", HttpsCertificateService.ReadCoveredNames(after));
    }

    [Fact]
    public void GetServerCertificate_KeepsTheCertificateForCoveredNames()
    {
        var service = CreateService();
        var first = service.GetServerCertificate("tsc.lan", null);

        var second = service.GetServerCertificate("TSC.lan", IPAddress.Loopback);
        var third = service.GetServerCertificate(null, IPAddress.Parse("192.168.178.93"));

        Assert.Equal(first.Thumbprint, second.Thumbprint);
        Assert.Equal(first.Thumbprint, third.Thumbprint);
    }

    [Fact]
    public void GetServerCertificate_StopsAddingRequestedNamesAtTheLimit()
    {
        var service = CreateService();
        var freeNames = HttpsCertificateService.MaxCoveredNames - LocalNames.Count;
        for (var i = 0; i < freeNames; i++)
        {
            service.GetServerCertificate($"host{i}.lan", null);
        }
        var full = service.GetServerCertificate(null, null);

        var afterLimit = service.GetServerCertificate("one-too-many.lan", null);

        Assert.Equal(HttpsCertificateService.MaxCoveredNames, HttpsCertificateService.ReadCoveredNames(full).Count);
        Assert.Equal(full.Thumbprint, afterLimit.Thumbprint);
        Assert.False(afterLimit.MatchesHostname("one-too-many.lan"));
    }

    [Fact]
    public void GetServerCertificate_ReissuesWhenAConfiguredNameIsAdded()
    {
        var service = CreateService();
        var before = service.GetServerCertificate(null, null);

        ConfiguredNames = ["new.lan",];
        var after = service.GetServerCertificate(null, null);

        Assert.NotEqual(before.Thumbprint, after.Thumbprint);
        Assert.True(after.MatchesHostname("new.lan"));
    }

    [Fact]
    public void GetServerCertificate_RenewsTheCertificateBeforeItExpires()
    {
        var service = CreateService();
        var root = GetRoot(service);
        var before = service.GetServerCertificate(null, null);

        Now += HttpsCertificateService.ServerValidity - HttpsCertificateService.RenewalLeadTime + TimeSpan.FromDays(1);
        var after = service.GetServerCertificate(null, null);

        Assert.NotEqual(before.Thumbprint, after.Thumbprint);
        Assert.True(after.NotAfter > before.NotAfter);
        Assert.Equal(root.Thumbprint, GetRoot(service).Thumbprint);
        Assert.True(ChainsTo(after, root));
    }

    [Fact]
    public void GetServerCertificate_KeepsTheCertificateUntilTheRenewalLeadTime()
    {
        var service = CreateService();
        var before = service.GetServerCertificate(null, null);

        Now += HttpsCertificateService.ServerValidity - HttpsCertificateService.RenewalLeadTime - TimeSpan.FromDays(1);

        Assert.Equal(before.Thumbprint, service.GetServerCertificate(null, null).Thumbprint);
    }

    [Fact]
    public void ANewInstanceKeepsTheRootTheServerCertificateAndTheRequestedNames()
    {
        var first = CreateService();
        var root = GetRoot(first);
        var server = first.GetServerCertificate("tsc.lan", null);

        var restarted = CreateService();

        Assert.Equal(root.Thumbprint, GetRoot(restarted).Thumbprint);
        var serverAfterRestart = restarted.GetServerCertificate(null, null);
        Assert.Equal(server.Thumbprint, serverAfterRestart.Thumbprint);
        Assert.True(serverAfterRestart.HasPrivateKey);
        Assert.True(serverAfterRestart.MatchesHostname("tsc.lan"));
    }

    [Fact]
    public void ANewInstanceRecreatesTheRootWhenItsKeyIsMissing()
    {
        var first = CreateService();
        var oldRoot = GetRoot(first);
        first.GetServerCertificate(null, null);
        File.Delete(Path.Combine(CertificateDirectory, HttpsCertificateService.RootKeyFileName));

        var restarted = CreateService();
        var newRoot = GetRoot(restarted);
        var server = restarted.GetServerCertificate(null, null);

        Assert.NotEqual(oldRoot.Thumbprint, newRoot.Thumbprint);
        Assert.True(ChainsTo(server, newRoot));
        Assert.False(ChainsTo(server, oldRoot));
    }

    [Fact]
    public void ANewInstanceRecreatesTheRootWhenTheFilesAreBroken()
    {
        var first = CreateService();
        var oldRoot = GetRoot(first);
        File.WriteAllText(Path.Combine(CertificateDirectory, HttpsCertificateService.RootCertificateFileName), "not a certificate");

        var newRoot = GetRoot(CreateService());

        Assert.NotEqual(oldRoot.Thumbprint, newRoot.Thumbprint);
    }

    [Fact]
    public void TheRootIsRecreatedShortlyBeforeItExpires()
    {
        var service = CreateService();
        var oldRoot = GetRoot(service);

        Now += HttpsCertificateService.RootValidity - HttpsCertificateService.RenewalLeadTime + TimeSpan.FromDays(1);
        var newRoot = GetRoot(service);
        var server = service.GetServerCertificate(null, null);

        Assert.NotEqual(oldRoot.Thumbprint, newRoot.Thumbprint);
        Assert.True(ChainsTo(server, newRoot));
    }

    [Fact]
    public void TheServerCertificateNeverOutlivesTheRoot()
    {
        var service = CreateService();
        var root = GetRoot(service);

        //Shortly before the root would be renewed, the server certificate is issued anew and ends with the root
        Now += HttpsCertificateService.RootValidity - HttpsCertificateService.RenewalLeadTime - TimeSpan.FromDays(2);
        var server = service.GetServerCertificate(null, null);

        Assert.Equal(root.NotAfter, server.NotAfter);
        Assert.True(ChainsTo(server, root));
    }

    [Fact]
    public void GetServerCertificate_StaysWithinTheRootsValidityWhenTheClockWentBack()
    {
        var service = CreateService();
        var root = GetRoot(service);

        Now -= TimeSpan.FromDays(3);
        var server = service.GetServerCertificate("later.lan", null);

        Assert.True(server.NotBefore >= root.NotBefore);
        Assert.True(server.MatchesHostname("later.lan"));
    }

    [Fact]
    public void RefusesToCreateCertificatesBeforeTheClockIsSet()
    {
        Now = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var service = CreateService();

        Assert.Throws<InvalidOperationException>(() => service.GetServerCertificate(null, null));
        Assert.Throws<InvalidOperationException>(() => service.GetRootCertificateDer());
        Assert.False(Directory.Exists(CertificateDirectory));
    }

    [Fact]
    public void TheKeyFilesAreOnlyAccessibleByTheOwner()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file permissions only");
        CreateService().GetServerCertificate(null, null);

        foreach (var keyFileName in new[] { HttpsCertificateService.RootKeyFileName, HttpsCertificateService.ServerKeyFileName, })
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(CertificateDirectory, keyFileName)));
        }
    }

    [Theory]
    [InlineData(7191, "http://[::]:7190", "https://[::]:7191")]
    [InlineData(5001, "https://localhost:5001/")]
    [InlineData(8443, "HTTPS://0.0.0.0:8443")]
    [InlineData(null, "http://[::]:7190")]
    [InlineData(null)]
    public void GetHttpsPort_ReadsThePortOfTheFirstHttpsAddress(int? expectedPort, params string[] serverAddresses)
    {
        Assert.Equal(expectedPort, HttpsCertificateService.GetHttpsPort(serverAddresses));
    }

    [Fact]
    public void GetHttpsInformation_DescribesTheCertificates()
    {
        var service = CreateService();
        var root = GetRoot(service);

        var information = service.GetHttpsInformation(["http://[::]:7190", "https://[::]:7191",]);

        Assert.True(information.IsEnabled);
        Assert.Equal(7191, information.Port);
        Assert.Equal(LocalNames.Order(StringComparer.Ordinal), information.CoveredNames);
        Assert.Equal(root.GetNameInfo(X509NameType.SimpleName, false), information.RootCertificateName);
        Assert.Matches("^([0-9A-F]{2}:){31}[0-9A-F]{2}$", information.RootCertificateSha256Fingerprint);
        Assert.Equal(root.NotAfter.ToUniversalTime(), information.RootCertificateValidUntil!.Value.UtcDateTime);
    }

    [Fact]
    public void GetHttpsInformation_IsDisabledWithoutHttpsAddress()
    {
        var information = CreateService().GetHttpsInformation(["http://[::]:7190",]);

        Assert.False(information.IsEnabled);
        Assert.Null(information.Port);
        //The certificate can be installed anyway, e.g. before HTTPS is switched on
        Assert.NotNull(information.RootCertificateSha256Fingerprint);
    }
}
