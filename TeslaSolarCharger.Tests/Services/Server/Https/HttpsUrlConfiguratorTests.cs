using System;
using System.Collections.Generic;
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
    [InlineData(" ; ")]
    public void Decide_ListensOnPort7190AndTheLegacyPort80WithoutConfiguredUrls(string? configuredUrls)
    {
        var decision = HttpsUrlConfigurator.Decide(configuredUrls, 7443, Available);

        Assert.True(decision.UsesDefaultUrls);
        Assert.Equal(HttpsUrlOutcome.Added, decision.Outcome);
        Assert.Equal("http://+:7190;http://+:80;https://+:7443", decision.Urls);
    }

    [Fact]
    public void Decide_LeavesOutPort80WhenItIsInUse()
    {
        //e.g. a web server on the host when TSC uses network_mode: host
        var decision = HttpsUrlConfigurator.Decide(null, 7443, port => port != 80);

        Assert.Equal(HttpsUrlOutcome.Added, decision.Outcome);
        Assert.Equal("http://+:7190;https://+:7443", decision.Urls);
    }

    [Fact]
    public void Decide_ListensOnTheDefaultUrlsWithHttpsDisabled()
    {
        var decision = HttpsUrlConfigurator.Decide(null, 0, Available);

        Assert.Equal(HttpsUrlOutcome.Disabled, decision.Outcome);
        Assert.Equal("http://+:7190;http://+:80", decision.Urls);
        Assert.True(decision.UsesDefaultUrls);
    }

    [Fact]
    public void Decide_ListensOnPort7190OnlyWhenPort80AndTheHttpsPortAreInUse()
    {
        var decision = HttpsUrlConfigurator.Decide(null, 7443, _ => false);

        Assert.Equal(HttpsUrlOutcome.PortUnavailable, decision.Outcome);
        Assert.Equal("http://+:7190", decision.Urls);
    }

    [Theory]
    [InlineData(7190)]
    [InlineData(80)]
    public void Decide_SkipsAnHttpsPortADefaultUrlUses(int httpsPort)
    {
        var decision = HttpsUrlConfigurator.Decide(null, httpsPort, Available);

        Assert.Equal(HttpsUrlOutcome.PortUnavailable, decision.Outcome);
        Assert.Equal("http://+:7190;http://+:80", decision.Urls);
    }

    [Fact]
    public void Decide_KeepsConfiguredUrlsWithoutAddingTheDefaultOnes()
    {
        var checkedPorts = new List<int>();

        var decision = HttpsUrlConfigurator.Decide("http://+:5021", 7443, port =>
        {
            checkedPorts.Add(port);
            return true;
        });

        Assert.False(decision.UsesDefaultUrls);
        Assert.Equal("http://+:5021;https://+:7443", decision.Urls);
        Assert.Equal([7443,], checkedPorts);
    }

    [Theory]
    [InlineData("http://+:7190;https://+:8443")]
    [InlineData("HTTPS://localhost:5001")]
    public void Decide_KeepsAnHttpsUrlTheUserConfigured(string configuredUrls)
    {
        var decision = HttpsUrlConfigurator.Decide(configuredUrls, 7443, Available);

        Assert.Equal(HttpsUrlOutcome.AlreadyConfigured, decision.Outcome);
        Assert.Equal(configuredUrls, decision.Urls);
    }

    [Theory]
    [InlineData("http://+:7190", "http://+:7190;https://+:7443")]
    [InlineData("http://+:80;http://+:7190", "http://+:80;http://+:7190;https://+:7443")]
    [InlineData(" http://+:7190 ; ", "http://+:7190;https://+:7443")]
    public void Decide_AddsTheHttpsUrl(string configuredUrls, string expectedUrls)
    {
        var decision = HttpsUrlConfigurator.Decide(configuredUrls, 7443, Available);

        Assert.Equal(HttpsUrlOutcome.Added, decision.Outcome);
        Assert.Equal(expectedUrls, decision.Urls);
        Assert.Equal(7443, decision.Port);
    }

    [Fact]
    public void Decide_SkipsAPortInUse()
    {
        var decision = HttpsUrlConfigurator.Decide("http://+:7190", 7443, _ => false);

        Assert.Equal(HttpsUrlOutcome.PortUnavailable, decision.Outcome);
        Assert.Equal("http://+:7190", decision.Urls);
    }

    [Theory]
    [InlineData("http://+:7443")]
    [InlineData("http://+:7190;http://localhost:7443/")]
    public void Decide_SkipsAPortAConfiguredUrlUses(string configuredUrls)
    {
        //Not bound yet, so the port still looks free
        Assert.Equal(HttpsUrlOutcome.PortUnavailable, HttpsUrlConfigurator.Decide(configuredUrls, 7443, Available).Outcome);
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
    [InlineData(HttpsUrlOutcome.PortUnavailable, LogLevel.Warning)]
    public void LogDecision_WarnsOnlyWhenTheHttpsPortIsInUse(HttpsUrlOutcome outcome, LogLevel expectedLevel)
    {
        var logger = new Mock<ILogger>();

        HttpsUrlConfigurator.LogDecision(logger.Object, new HttpsUrlDecision(outcome, "http://+:7190", 7443, false));

        logger.Verify(l => l.Log(expectedLevel, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void LogDecision_NamesTheUrlsOnlyWhenTheDefaultOnesAreUsed(bool usesDefaultUrls, int expectedCount)
    {
        var logger = new Mock<ILogger>();

        HttpsUrlConfigurator.LogDecision(logger.Object,
            new HttpsUrlDecision(HttpsUrlOutcome.Added, "http://+:7190;https://+:7443", 7443, usesDefaultUrls));

        logger.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("http://+:7190;https://+:7443")), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(expectedCount));
    }
}
