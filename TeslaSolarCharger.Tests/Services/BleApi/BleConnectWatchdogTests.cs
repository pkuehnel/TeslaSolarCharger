using PkSoftwareService.Custom.Backend.Ble;
using System;
using TeslaSolarCharger.BleApi.Services;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.BleApi;

/// <summary>
/// The watchdog for the failure this replaced the deafness restart with: a radio that hears a car perfectly well and
/// still cannot open a connection to it.
///
/// Measured on the live system, a worker gets into that state and stays there - ten connect attempts in a row timing
/// out at their full ten seconds while the car sat in the driveway at -46 dBm - and only a worker restart clears it,
/// after which the next connect took 752 ms. Until the deafness check was corrected, the restarts it fired for the
/// wrong reason happened to keep this from being noticed.
/// </summary>
public class BleConnectWatchdogTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 13, 30, 0, TimeSpan.Zero);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// <summary>Only a car that was heard and still could not be reached counts against the radio.</summary>
    [Fact]
    public void FailingToReachACarThatIsBeingHeardBuildsTheStreak()
    {
        var streak = 0;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            streak = BleWorkerService.NextUnreachableStreak(streak, BleCommandOutcome.LinkFailed, heardRecently: true);
            Assert.Equal(attempt, streak);
        }
    }

    /// <summary>
    /// A car that is not being heard is simply gone, which says nothing about the radio and must never restart a
    /// working worker.
    /// </summary>
    [Fact]
    public void FailingToReachACarThatIsNotHeardDoesNotCount()
    {
        Assert.Equal(3, BleWorkerService.NextUnreachableStreak(3, BleCommandOutcome.LinkFailed, heardRecently: false));
    }

    /// <summary>Anything the car answered proves the link, so it clears the streak - a refusal as much as a success.</summary>
    [Theory]
    [InlineData(BleCommandOutcome.Ok)]
    [InlineData(BleCommandOutcome.CarRefused)]
    [InlineData(BleCommandOutcome.CarAsleep)]
    public void AnAnswerFromTheCarClearsTheStreak(BleCommandOutcome outcome)
    {
        Assert.Equal(0, BleWorkerService.NextUnreachableStreak(4, outcome, heardRecently: true));
    }

    /// <summary>Outcomes that never got as far as the link neither count nor clear.</summary>
    [Theory]
    [InlineData(BleCommandOutcome.AdapterNotFound)]
    [InlineData(BleCommandOutcome.AdapterUnavailable)]
    [InlineData(BleCommandOutcome.WorkerError)]
    [InlineData(BleCommandOutcome.CarAbsent)]
    [InlineData(null)]
    public void OutcomesThatSayNothingAboutTheLinkLeaveTheStreakAlone(BleCommandOutcome? outcome)
    {
        Assert.Equal(2, BleWorkerService.NextUnreachableStreak(2, outcome, heardRecently: true));
    }

    /// <summary>
    /// Short runs of connect failures happen on this hardware and clear up by themselves - four in a row, then a
    /// connect in 817 ms, was measured. The threshold sits above that so ordinary flakiness costs no restart.
    /// </summary>
    [Fact]
    public void AShortRunOfFailuresDoesNotRestartTheWorker()
    {
        Assert.False(BleWorkerService.ShouldRestartForUnreachable(4, 5, DateTimeOffset.MinValue, Cooldown, Now));
    }

    [Fact]
    public void AStreakAtTheThresholdRestartsTheWorker()
    {
        Assert.True(BleWorkerService.ShouldRestartForUnreachable(5, 5, DateTimeOffset.MinValue, Cooldown, Now));
    }

    /// <summary>
    /// A car that refuses connections for its own reasons would otherwise restart the worker on every sweep.
    /// </summary>
    [Fact]
    public void TheCooldownKeepsAHopelessCaseFromRestartingEverySweep()
    {
        Assert.False(BleWorkerService.ShouldRestartForUnreachable(9, 5, Now.AddSeconds(-59), Cooldown, Now));
        Assert.True(BleWorkerService.ShouldRestartForUnreachable(9, 5, Now.AddSeconds(-60), Cooldown, Now));
    }

    [Fact]
    public void AThresholdOfZeroTurnsTheWatchdogOff()
    {
        Assert.False(BleWorkerService.ShouldRestartForUnreachable(100, 0, DateTimeOffset.MinValue, Cooldown, Now));
    }
}
