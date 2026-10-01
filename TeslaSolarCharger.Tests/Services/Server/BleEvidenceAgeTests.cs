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

    /// <summary>
    /// Whenever an age is concluded there is nothing to explain, so the log never pairs a usable age with a reason.
    /// </summary>
    [Theory]
    [InlineData(4L, true, false)]
    [InlineData(90000L, true, true)]
    [InlineData(600000L, false, true)]
    public void AConcludedAgeCarriesNoUnknownReason(long lastSeenMsAgo, bool warmingUp, bool scannerRunning)
    {
        var evidence = BleVehicleDataService.EvaluateEvidence(Presence(warmingUp, scannerRunning), Vehicle(lastSeenMsAgo), MaxAge);

        Assert.Equal(TimeSpan.FromMilliseconds(lastSeenMsAgo), evidence.Age);
        Assert.Null(evidence.UnknownReason);
    }

    [Fact]
    public void ACarMissingFromTheAnswerIsReportedAsNotReported()
    {
        var evidence = BleVehicleDataService.EvaluateEvidence(Presence(warmingUp: false, scannerRunning: true), null, MaxAge);

        Assert.Null(evidence.Age);
        Assert.Equal("the BLE container did not report the car at all", evidence.UnknownReason);
    }

    /// <summary>
    /// The case that hid a deaf radio for hours: a settled, running scan that simply never heard the car. It must read
    /// differently from a warm-up, which is the harmless reason for the same null age.
    /// </summary>
    [Fact]
    public void ACarNeverHeardIsReportedAsNeverHeardEvenFromASettledScan()
    {
        var evidence = BleVehicleDataService.EvaluateEvidence(Presence(warmingUp: false, scannerRunning: true), Vehicle(null), MaxAge);

        Assert.Null(evidence.Age);
        Assert.Contains("never heard the car since it started", evidence.UnknownReason);
    }

    [Fact]
    public void StaleEvidenceWithAStoppedScanNamesTheScanError()
    {
        var presence = Presence(warmingUp: false, scannerRunning: false);
        presence.LastScanError = "adapter powered off";

        var evidence = BleVehicleDataService.EvaluateEvidence(presence, Vehicle(600000), MaxAge);

        Assert.Null(evidence.Age);
        Assert.Equal("the car was last heard 00:10:00 ago and the BLE scan is not running (last scan error: adapter powered off)",
            evidence.UnknownReason);
    }

    [Fact]
    public void StaleEvidenceWithAStoppedScanAndNoErrorOmitsTheErrorPart()
    {
        var evidence = BleVehicleDataService.EvaluateEvidence(Presence(warmingUp: false, scannerRunning: false), Vehicle(600000), MaxAge);

        Assert.Equal("the car was last heard 00:10:00 ago and the BLE scan is not running", evidence.UnknownReason);
    }

    /// <summary>A scan that is not running is the more fundamental reason, so it wins over warming up.</summary>
    [Fact]
    public void AStoppedScanIsReportedBeforeAWarmUp()
    {
        var evidence = BleVehicleDataService.EvaluateEvidence(Presence(warmingUp: true, scannerRunning: false), Vehicle(600000), MaxAge);

        Assert.Contains("the BLE scan is not running", evidence.UnknownReason);
    }

    [Fact]
    public void StaleEvidenceDuringAWarmUpNamesHowLongTheScanHasBeenObserving()
    {
        var presence = Presence(warmingUp: true, scannerRunning: true);
        presence.ObservingMs = 42400;

        var evidence = BleVehicleDataService.EvaluateEvidence(presence, Vehicle(600000), MaxAge);

        Assert.Null(evidence.Age);
        Assert.Equal("the car was last heard 00:10:00 ago and the BLE scan is still warming up (observing for 00:00:42)",
            evidence.UnknownReason);
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
