using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using TeslaSolarCharger.Server.Services;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// The rule that decides whether a presence answer may be acted on at all, tested as the pure function it is.
///
/// It exists because reading the container's self report before its evidence made a car unreachable in blocks: the
/// deaf adapter watchdog restarted the worker every few minutes, each restart set warming up for a full max age, and
/// during that window advertisements that were milliseconds old were thrown away and the car was declared unknown.
/// </summary>
public class BleEvidenceAgeTests
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(90);

    private static DtoBlePresenceResult Presence(bool warmingUp, bool scannerRunning) => new()
    {
        WarmingUp = warmingUp,
        ScannerRunning = scannerRunning,
        MaxAgeMs = (long)MaxAge.TotalMilliseconds,
        Vehicles = new List<DtoBlePresenceVehicle>(),
    };

    private static DtoBlePresenceVehicle Vehicle(long? lastSeenMsAgo) => new() { LastSeenMsAgo = lastSeenMsAgo, };

    /// <summary>
    /// Evidence within the max age proves the car is here however long the scan has been observing.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void FreshEvidenceIsAcceptedWhateverTheScannerReportsAboutItself(bool warmingUp, bool scannerRunning)
    {
        var age = BleVehicleDataService.EvidenceAge(Presence(warmingUp, scannerRunning), Vehicle(4), MaxAge);
        Assert.Equal(TimeSpan.FromMilliseconds(4), age);
    }

    /// <summary>
    /// Evidence exactly at the boundary still counts, because the caller compares with the same max age.
    /// </summary>
    [Fact]
    public void EvidenceExactlyAtTheMaxAgeIsStillAccepted()
    {
        var age = BleVehicleDataService.EvidenceAge(Presence(warmingUp: true, scannerRunning: true), Vehicle(90000), MaxAge);
        Assert.Equal(MaxAge, age);
    }

    /// <summary>
    /// The other half of the rule: once the evidence is stale the flags matter again, because then the answer is
    /// ignorance rather than absence and nothing may be concluded.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void StaleEvidenceIsDiscardedWhileTheScannerCannotVouchForItself(bool warmingUp, bool scannerRunning)
    {
        Assert.Null(BleVehicleDataService.EvidenceAge(Presence(warmingUp, scannerRunning), Vehicle(600000), MaxAge));
    }

    /// <summary>
    /// A settled scanner that has been observing longer than the max age is the only one whose silence is evidence of
    /// absence, so its stale answer is passed on for the away machinery to act on.
    /// </summary>
    [Fact]
    public void StaleEvidenceFromASettledScannerIsAbsence()
    {
        var age = BleVehicleDataService.EvidenceAge(Presence(warmingUp: false, scannerRunning: true), Vehicle(600000), MaxAge);
        Assert.Equal(TimeSpan.FromMinutes(10), age);
    }

    /// <summary>
    /// A car the container has no record of at all was never heard, which is not the same as not being there.
    /// </summary>
    [Fact]
    public void ACarWithNoRecordAtAllConcludesNothing()
    {
        Assert.Null(BleVehicleDataService.EvidenceAge(Presence(warmingUp: false, scannerRunning: true), Vehicle(null), MaxAge));
        Assert.Null(BleVehicleDataService.EvidenceAge(Presence(warmingUp: false, scannerRunning: true), null, MaxAge));
    }
}
