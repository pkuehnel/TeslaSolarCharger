using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using TeslaSolarCharger.Server.Services;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// Home when heard within the max age; away when not heard and the scan has been running for at least the max age;
/// otherwise unknown (keep the last state).
/// </summary>
public class BlePresenceRuleTests
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(180);

    private static DtoBlePresenceResult Presence(bool warmingUp, bool scannerRunning) => new()
    {
        WarmingUp = warmingUp,
        ScannerRunning = scannerRunning,
        MaxAgeMs = (long)MaxAge.TotalMilliseconds,
        Vehicles = new List<DtoBlePresenceVehicle>(),
    };

    private static DtoBlePresenceVehicle Vehicle(long? lastSeenMsAgo) => new() { LastSeenMsAgo = lastSeenMsAgo, };

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void ACarHeardWithinTheMaxAgeIsPresentWhateverTheScanReports(bool warmingUp, bool scannerRunning)
    {
        Assert.True(BleVehicleDataService.IsPresent(Presence(warmingUp, scannerRunning), Vehicle(4), MaxAge));
    }

    [Fact]
    public void TheMaxAgeItselfStillCountsAsPresent()
    {
        Assert.True(BleVehicleDataService.IsPresent(Presence(false, true), Vehicle(180_000), MaxAge));
        Assert.False(BleVehicleDataService.IsPresent(Presence(false, true), Vehicle(180_001), MaxAge));
    }

    [Fact]
    public void ACarNotHeardWithinTheMaxAgeOfAWarmScanIsAway()
    {
        Assert.False(BleVehicleDataService.IsPresent(Presence(false, true), Vehicle(600_000), MaxAge));
    }

    /// <summary>The Franzi case: the container restarted while the car was away and never heard it since.</summary>
    [Fact]
    public void ACarNeverHeardByAWarmScanIsAway()
    {
        Assert.False(BleVehicleDataService.IsPresent(Presence(false, true), Vehicle(null), MaxAge));
        Assert.False(BleVehicleDataService.IsPresent(Presence(false, true), null, MaxAge));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void ACarNotHeardIsUnknownWhileTheScanIsWarmingUpOrNotRunning(bool warmingUp, bool scannerRunning)
    {
        Assert.Null(BleVehicleDataService.IsPresent(Presence(warmingUp, scannerRunning), Vehicle(600_000), MaxAge));
        Assert.Null(BleVehicleDataService.IsPresent(Presence(warmingUp, scannerRunning), Vehicle(null), MaxAge));
    }

    [Theory]
    [InlineData(0L, "00:00:00")]
    [InlineData(499L, "00:00:00")]
    [InlineData(1500L, "00:00:02")]
    [InlineData(50_400_000L, "14:00:00")]
    [InlineData(93_784_000L, "1.02:03:04")]
    public void DurationsAreFormattedInWholeSeconds(long milliseconds, string expected)
    {
        Assert.Equal(expected, BleVehicleDataService.FormatDuration(TimeSpan.FromMilliseconds(milliseconds)));
        Assert.Equal(expected, BleVehicleDataService.FormatMsAgo(milliseconds));
    }

    [Fact]
    public void AMissingTimestampIsFormattedAsNever()
    {
        Assert.Equal("never", BleVehicleDataService.FormatMsAgo(null));
    }
}
