using System;
using TeslaSolarCharger.Client.Helper;
using TeslaSolarCharger.Shared.Dtos.Https;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Client;

public class HttpsUrlHelperTests
{
    [Theory]
    [InlineData("https://primary-pc:7443/", true)]
    [InlineData("HTTPS://primary-pc/", true)]
    [InlineData("http://primary-pc:7190/", false)]
    public void IsSecure_IsTrueOnlyForHttps(string uri, bool expected)
    {
        Assert.Equal(expected, HttpsUrlHelper.IsSecure(new Uri(uri)));
    }

    [Theory]
    [InlineData("http://primary-pc:7190/", 7443, "https://primary-pc:7443/")]
    [InlineData("http://192.168.178.93:7190/BaseConfiguration?tab=1#top", 7443, "https://192.168.178.93:7443/BaseConfiguration?tab=1#top")]
    [InlineData("http://[fd00::5]:7190/", 8443, "https://[fd00::5]:8443/")]
    [InlineData("http://tsc.lan/", 7443, "https://tsc.lan:7443/")]
    public void ToHttpsUri_KeepsHostAndPageAndSwitchesSchemeAndPort(string uri, int httpsPort, string expected)
    {
        Assert.Equal(new Uri(expected), HttpsUrlHelper.ToHttpsUri(new Uri(uri), httpsPort));
    }

    [Fact]
    public void ToHttpsUri_DropsTheDefaultPort()
    {
        Assert.Equal("https://tsc.lan/", HttpsUrlHelper.ToHttpsUri(new Uri("http://tsc.lan:7190/"), 443).ToString());
    }

    [Theory]
    //Over HTTP behind a port mapping it cannot be seen whether the HTTPS port is published as well
    [InlineData(true, HttpsReachability.Forwarded, "http://192.168.178.43:7190/", true)]
    //A page loaded over HTTPS proves the port is reachable
    [InlineData(true, HttpsReachability.Forwarded, "https://192.168.178.43:7443/", false)]
    //The browser talks to Solar4Car directly or there is nothing to go by
    [InlineData(true, HttpsReachability.Reachable, "http://192.168.178.43:7190/", false)]
    [InlineData(true, HttpsReachability.Unknown, "http://192.168.178.43:7190/", false)]
    //Without HTTPS there is no port to publish
    [InlineData(false, HttpsReachability.Forwarded, "http://192.168.178.43:7190/", false)]
    public void ShowsPortPublishingHint_OnlyOverHttpBehindAPortForwarding(bool isEnabled, HttpsReachability reachability, string currentUri, bool expected)
    {
        var httpsInformation = new DtoHttpsInformation { IsEnabled = isEnabled, Port = 7443, Reachability = reachability, };

        Assert.Equal(expected, HttpsUrlHelper.ShowsPortPublishingHint(httpsInformation, new Uri(currentUri)));
    }
}
