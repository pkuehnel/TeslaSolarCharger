using System.Net;
using TeslaSolarCharger.Shared.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Shared;

public class HttpsHostNameHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ,; ")]
    public void SplitHostNames_ReturnsNothingForEmptyInput(string? hostNames)
    {
        Assert.Empty(HttpsHostNameHelper.SplitHostNames(hostNames));
    }

    [Fact]
    public void SplitHostNames_AcceptsLinesCommasSemicolonsAndSpaces()
    {
        var hostNames = HttpsHostNameHelper.SplitHostNames("a.lan\r\nB.LAN, c.lan; d.lan  e.lan\tf.lan\ng.lan");

        Assert.Equal(["a.lan", "b.lan", "c.lan", "d.lan", "e.lan", "f.lan", "g.lan",], hostNames);
    }

    [Fact]
    public void SplitHostNames_RemovesDuplicatesAfterNormalizing()
    {
        var hostNames = HttpsHostNameHelper.SplitHostNames("tsc.lan\nTSC.lan.\n192.168.1.5\n::ffff:192.168.1.5");

        Assert.Equal(["tsc.lan", "192.168.1.5",], hostNames);
    }

    [Fact]
    public void SplitHostNames_KeepsInvalidEntries()
    {
        //So the validator can name them
        Assert.Equal(["bad_name",], HttpsHostNameHelper.SplitHostNames("bad_name"));
    }

    [Theory]
    [InlineData("Primary-PC", "primary-pc")]
    [InlineData("tsc.fritz.box.", "tsc.fritz.box")]
    [InlineData(" tsc.lan ", "tsc.lan")]
    [InlineData("[::1]", "::1")]
    [InlineData("::ffff:192.168.178.93", "192.168.178.93")]
    [InlineData("FE80:0:0:0:0:0:0:1", "fe80::1")]
    [InlineData("müller.lan", "xn--mller-kva.lan")]
    public void Normalize_BringsNamesIntoTheFormBrowsersSend(string hostName, string expected)
    {
        Assert.Equal(expected, HttpsHostNameHelper.Normalize(hostName));
    }

    [Theory]
    [InlineData("1.2.3.4", true)]
    [InlineData("192.168.178.93", true)]
    [InlineData("::1", true)]
    [InlineData("2001:db8::5", true)]
    [InlineData("123", false)]
    [InlineData("1.2.3", false)]
    [InlineData("256.1.1.1", false)]
    [InlineData("tsc.lan", false)]
    public void TryParseIpAddress_OnlyAcceptsCompleteAddresses(string value, bool expected)
    {
        Assert.Equal(expected, HttpsHostNameHelper.TryParseIpAddress(value, out _));
    }

    [Fact]
    public void TryParseIpAddress_ReturnsMappedIpv4AddressesAsIpv4()
    {
        Assert.True(HttpsHostNameHelper.TryParseIpAddress("::ffff:10.0.0.5", out var ipAddress));
        Assert.Equal(IPAddress.Parse("10.0.0.5"), ipAddress);
    }

    [Fact]
    public void NormalizeIpAddress_DropsTheScopeOfIpv6Addresses()
    {
        var withScope = IPAddress.Parse("fe80::1%3");

        Assert.Equal("fe80::1", HttpsHostNameHelper.NormalizeIpAddress(withScope).ToString());
    }

    [Theory]
    [InlineData("primary-pc")]
    [InlineData("tsc.fritz.box")]
    [InlineData("my-host1.home.arpa")]
    [InlineData("xn--mller-kva.lan")]
    [InlineData("192.168.178.93")]
    [InlineData("::1")]
    [InlineData("a")]
    public void IsValidHostName_AcceptsHostNamesAndIpAddresses(string hostName)
    {
        Assert.True(HttpsHostNameHelper.IsValidHostName(hostName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad_name")]
    [InlineData("-start.lan")]
    [InlineData("end-.lan")]
    [InlineData("a..b")]
    [InlineData("192.168.1")]
    [InlineData("123")]
    [InlineData("256.1.1.1")]
    [InlineData("https://tsc.lan")]
    [InlineData("tsc.lan:7443")]
    [InlineData("Upper.lan")]
    public void IsValidHostName_RejectsEverythingElse(string hostName)
    {
        Assert.False(HttpsHostNameHelper.IsValidHostName(hostName));
    }

    [Fact]
    public void IsValidHostName_RejectsTooLongLabelsAndNames()
    {
        var label63 = new string('a', 63);
        Assert.True(HttpsHostNameHelper.IsValidHostName(label63 + ".lan"));
        Assert.False(HttpsHostNameHelper.IsValidHostName(label63 + "a.lan"));

        var name253 = string.Join('.', label63, label63, label63, new string('b', 61));
        Assert.Equal(253, name253.Length);
        Assert.True(HttpsHostNameHelper.IsValidHostName(name253));
        Assert.False(HttpsHostNameHelper.IsValidHostName(name253 + "b"));
    }
}
