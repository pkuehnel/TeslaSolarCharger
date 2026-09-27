using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Server.Services.Https;
using TeslaSolarCharger.Server.Services.Https.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Https;

/// <summary>
/// Real TLS handshakes against Kestrel, wired up like Program.cs: an https URL without any configured certificate,
/// the certificate comes from <see cref="HttpsKestrelExtensions.UseTscHttpsCertificates"/>.
/// </summary>
public class HttpsKestrelIntegrationTests : HttpsCertificateTestBase
{
    public HttpsKestrelIntegrationTests()
    {
        //The client checks the certificate against the real clock
        Now = DateTimeOffset.UtcNow;
    }

    private async Task<(WebApplication App, HttpsCertificateService Service, int Port)> StartServerAsync()
    {
        var service = CreateService();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IHttpsCertificateService>(service);
        //Part of WebApplication.CreateBuilder, which Program.cs uses, but not of the slim builder
        builder.WebHost.UseKestrelHttpsConfiguration();
        builder.WebHost.UseUrls("https://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(options => options.UseTscHttpsCertificates());
        var app = builder.Build();
        app.MapGet("/", () => "ok");
        await app.StartAsync(TestContext.Current.CancellationToken);
        return (app, service, new Uri(app.Urls.Single()).Port);
    }

    /// <summary>
    /// Connects to the local server whatever host name the URL has, so the handshake requests that name.
    /// </summary>
    private static HttpClient CreateClient(int port, X509Certificate2? trustedRoot)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
                return new NetworkStream(socket, true);
            },
        };
        if (trustedRoot != default)
        {
            handler.SslOptions.CertificateChainPolicy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck,
                CustomTrustStore = { trustedRoot, },
            };
        }
        return new HttpClient(handler);
    }

    [Fact]
    public async Task ADeviceWithTheRootInstalledTrustsTheIpAddress()
    {
        var (app, service, port) = await StartServerAsync();
        await using var _ = app;
        using var client = CreateClient(port, GetRoot(service));

        var response = await client.GetStringAsync($"https://127.0.0.1:{port}/", TestContext.Current.CancellationToken);

        Assert.Equal("ok", response);
    }

    [Fact]
    public async Task ANewHostNameIsTrustedOnTheFirstRequest()
    {
        var (app, service, port) = await StartServerAsync();
        await using var _ = app;
        using var client = CreateClient(port, GetRoot(service));

        var response = await client.GetStringAsync($"https://tsc.home.arpa:{port}/", TestContext.Current.CancellationToken);

        Assert.Equal("ok", response);
        Assert.True(service.GetServerCertificate(null, null).MatchesHostname("tsc.home.arpa"));
    }

    [Fact]
    public async Task ADeviceWithoutTheRootDoesNotTrustTheCertificate()
    {
        var (app, _, port) = await StartServerAsync();
        await using var _ = app;
        using var client = CreateClient(port, null);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetStringAsync($"https://127.0.0.1:{port}/", TestContext.Current.CancellationToken));

        Assert.IsType<AuthenticationException>(exception.InnerException);
    }

    [Fact]
    public async Task ADeviceTrustingAnotherRootDoesNotTrustTheCertificate()
    {
        var (app, _, port) = await StartServerAsync();
        await using var _ = app;
        using var otherInstallation = new OtherInstallation();
        using var client = CreateClient(port, GetRoot(otherInstallation.Service));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetStringAsync($"https://127.0.0.1:{port}/", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A second installation with its own certificate directory and so its own root.
    /// </summary>
    private sealed class OtherInstallation : HttpsCertificateTestBase
    {
        public OtherInstallation()
        {
            Now = DateTimeOffset.UtcNow;
            Service = CreateService();
        }

        public HttpsCertificateService Service { get; }
    }
}
