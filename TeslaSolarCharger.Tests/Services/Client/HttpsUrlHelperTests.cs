using System;
using TeslaSolarCharger.Client.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Client;

public class HttpsUrlHelperTests
{
    [Theory]
    [InlineData("https://primary-pc:7191/", true)]
    [InlineData("HTTPS://primary-pc/", true)]
    [InlineData("http://primary-pc:7190/", false)]
    public void IsSecure_IsTrueOnlyForHttps(string uri, bool expected)
    {
        Assert.Equal(expected, HttpsUrlHelper.IsSecure(new Uri(uri)));
    }

    [Theory]
    [InlineData("http://primary-pc:7190/", 7191, "https://primary-pc:7191/")]
    [InlineData("http://192.168.178.93:7190/BaseConfiguration?tab=1#top", 7191, "https://192.168.178.93:7191/BaseConfiguration?tab=1#top")]
    [InlineData("http://[fd00::5]:7190/", 8443, "https://[fd00::5]:8443/")]
    [InlineData("http://tsc.lan/", 7191, "https://tsc.lan:7191/")]
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
