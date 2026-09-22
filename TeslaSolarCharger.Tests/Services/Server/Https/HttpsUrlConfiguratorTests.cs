using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services.Https;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Https;

public class HttpsUrlConfiguratorTests
{
    private static readonly Func<int, bool> Available = _ => true;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Decide_IsDisabledByAPortOfZeroOrLess(int httpsPort)
    {
        var decision = HttpsUrlConfigurator.Decide("http://+:7190", httpsPort, Available);

        Assert.Equal(HttpsUrlOutcome.Disabled, decision.Outcome);
        Assert.Equal("http://+:7190", decision.Urls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Decide_AddsNothingWithoutConfiguredUrls(string? configuredUrls)
    {
        //Without URLs Kestrel listens on its default, which an added URL would replace
        Assert.Equal(HttpsUrlOutcome.NoUrlsConfigured, HttpsUrlConfigurator.Decide(configuredUrls, 7191, Available).Outcome);
    }

    [Theory]
    [InlineData("http://+:7190;https://+:8443")]
    [InlineData("HTTPS://localhost:5001")]
    public void Decide_KeepsAnHttpsUrlTheUserConfigured(string configuredUrls)
    {
        var decision = HttpsUrlConfigurator.Decide(configuredUrls, 7191, Available);

        Assert.Equal(HttpsUrlOutcome.AlreadyConfigured, decision.Outcome);
        Assert.Equal(configuredUrls, decision.Urls);
    }

    [Theory]
    [InlineData("http://+:7190", "http://+:7190;https://+:7191")]
    [InlineData("http://+:80;http://+:7190", "http://+:80;http://+:7190;https://+:7191")]
    [InlineData(" http://+:7190 ; ", "http://+:7190;https://+:7191")]
    public void Decide_AddsTheHttpsUrl(string configuredUrls, string expectedUrls)
    {
        var decision = HttpsUrlConfigurator.Decide(configuredUrls, 7191, Available);

        Assert.Equal(HttpsUrlOutcome.Added, decision.Outcome);
        Assert.Equal(expectedUrls, decision.Urls);
        Assert.Equal(7191, decision.Port);
    }

    [Fact]
    public void Decide_SkipsAPortInUse()
    {
        var decision = HttpsUrlConfigurator.Decide("http://+:7190", 7191, _ => false);

        Assert.Equal(HttpsUrlOutcome.PortUnavailable, decision.Outcome);
        Assert.Equal("http://+:7190", decision.Urls);
    }

    [Theory]
    [InlineData("http://+:7191")]
    [InlineData("http://+:7190;http://localhost:7191/")]
    public void Decide_SkipsAPortAConfiguredUrlUses(string configuredUrls)
    {
        //Not bound yet, so the port still looks free
        Assert.Equal(HttpsUrlOutcome.PortUnavailable, HttpsUrlConfigurator.Decide(configuredUrls, 7191, Available).Outcome);
    }

    //Loopback instead of all addresses, as listening on all addresses makes Windows ask to allow it in the firewall
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsTcpPortAvailable_DetectsAPortInUse(bool occupiedOnIpv6)
    {
        Assert.SkipWhen(occupiedOnIpv6 && !Socket.OSSupportsIPv6, "IPv6 is not available");
        var listener = new TcpListener(occupiedOnIpv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            Assert.False(HttpsUrlConfigurator.IsTcpPortAvailable(port, IPAddress.IPv6Loopback, IPAddress.Loopback));
        }
        finally
        {
            listener.Stop();
        }

        Assert.True(HttpsUrlConfigurator.IsTcpPortAvailable(port, IPAddress.IPv6Loopback, IPAddress.Loopback));
    }

    [Theory]
    [InlineData(HttpsUrlOutcome.Added, LogLevel.Information)]
    [InlineData(HttpsUrlOutcome.AlreadyConfigured, LogLevel.Information)]
    [InlineData(HttpsUrlOutcome.Disabled, LogLevel.Information)]
    [InlineData(HttpsUrlOutcome.NoUrlsConfigured, LogLevel.Information)]
    [InlineData(HttpsUrlOutcome.PortUnavailable, LogLevel.Warning)]
    public void LogDecision_WarnsOnlyWhenTheHttpsPortIsInUse(HttpsUrlOutcome outcome, LogLevel expectedLevel)
    {
        var logger = new Mock<ILogger>();

        HttpsUrlConfigurator.LogDecision(logger.Object, new HttpsUrlDecision(outcome, "http://+:7190", 7191));

        logger.Verify(l => l.Log(expectedLevel, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
