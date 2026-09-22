using System;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TeslaSolarCharger.Server.Services.Https;
using TeslaSolarCharger.Shared.Helper;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Https;

public class LocalNetworkNameProviderTests
{
    [Fact]
    public void ContainsLocalhostTheLoopbackAddressAndTheHostName()
    {
        var names = new LocalNetworkNameProvider(NullLogger<LocalNetworkNameProvider>.Instance).GetLocalHostNamesAndAddresses();

        Assert.Contains("localhost", names);
        Assert.Contains("127.0.0.1", names);
        Assert.Contains(HttpsHostNameHelper.Normalize(Dns.GetHostName()), names);
    }

    [Fact]
    public void ContainsOnlyDistinctValidNamesWithoutLinkLocalIpv6Addresses()
    {
        var names = new LocalNetworkNameProvider(NullLogger<LocalNetworkNameProvider>.Instance).GetLocalHostNamesAndAddresses();

        Assert.All(names, name => Assert.True(HttpsHostNameHelper.IsValidHostName(name), name));
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(names, name => name.StartsWith("fe80:", StringComparison.Ordinal));
    }
}
