using System;
using TeslaSolarCharger.Client.Helper;
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
}
