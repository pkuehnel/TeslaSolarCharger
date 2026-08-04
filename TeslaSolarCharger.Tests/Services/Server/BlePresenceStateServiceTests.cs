using System;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BlePresenceStateServiceTests
{
    private const int CarId = 1;
    private const int OtherCarId = 2;
    //Suspend charging commands on the very first missed scan: the behaviour before the tolerance became configurable,
    //which most tests here still describe.
    private const int OnFirstMiss = 1;
    private static readonly TimeSpan Threshold = BlePresenceStateService.AwayConfirmationDuration;
    private static readonly DateTime Start = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);

    private static IBlePresenceStateService NewService()
        => new BlePresenceStateService(Mock.Of<ILogger<BlePresenceStateService>>());

    [Fact]
    public void ConfirmsAwayExactlyOnceAfterTheConfirmationDuration()
    {
        var service = NewService();
        Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(CarId, Start));
        //Still inside the confirmation window, no matter how many misses were observed.
        Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(CarId, Start + Threshold - TimeSpan.FromSeconds(1)));
        Assert.Equal(BleAwayConfirmation.JustConfirmed, service.RegisterOutOfRange(CarId, Start + Threshold));
        //Further out of range results while the car stays away must not re-fire the away transition.
        Assert.Equal(BleAwayConfirmation.AlreadyConfirmed, service.RegisterOutOfRange(CarId, Start + Threshold + TimeSpan.FromSeconds(13)));
        Assert.Equal(BleAwayConfirmation.AlreadyConfirmed, service.RegisterOutOfRange(CarId, Start + Threshold + TimeSpan.FromHours(5)));
    }

    [Fact]
    public void DoesNotConfirmAwayOnASingleMissAfterAStalledPoller()
    {
        var service = NewService();
        //A poller that stopped for longer than the confirmation duration must not confirm a car as away on its very
        //first poll after resuming: elapsed time alone is not evidence of a continuously missing car.
        Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(CarId, Start));
        Assert.Equal(BleAwayConfirmation.JustConfirmed, service.RegisterOutOfRange(CarId, Start + Threshold + TimeSpan.FromHours(1)));
    }

    [Fact]
    public void ConfirmationTimeIsIndependentOfThePollInterval()
    {
        //The whole point of measuring a duration instead of counting misses: halving the poll interval must not halve
        //the time a car has to be gone before it counts as away.
        var fastPoller = NewService();
        for (var elapsed = TimeSpan.Zero; elapsed < Threshold; elapsed += TimeSpan.FromSeconds(5))
        {
            Assert.Equal(BleAwayConfirmation.NotConfirmed, fastPoller.RegisterOutOfRange(CarId, Start + elapsed));
        }
        Assert.Equal(BleAwayConfirmation.JustConfirmed, fastPoller.RegisterOutOfRange(CarId, Start + Threshold));

        var slowPoller = NewService();
        for (var elapsed = TimeSpan.Zero; elapsed < Threshold; elapsed += TimeSpan.FromSeconds(30))
        {
            Assert.Equal(BleAwayConfirmation.NotConfirmed, slowPoller.RegisterOutOfRange(CarId, Start + elapsed));
        }
        Assert.Equal(BleAwayConfirmation.JustConfirmed, slowPoller.RegisterOutOfRange(CarId, Start + Threshold));
    }

    [Fact]
    public void PresenceIsUncertainOnlyBetweenFirstFailureAndAwayConfirmation()
    {
        var service = NewService();
        Assert.False(service.IsPresenceUncertain(CarId, OnFirstMiss));
        service.RegisterOutOfRange(CarId, Start);
        Assert.True(service.IsPresenceUncertain(CarId, OnFirstMiss));
        service.RegisterOutOfRange(CarId, Start + Threshold - TimeSpan.FromSeconds(1));
        Assert.True(service.IsPresenceUncertain(CarId, OnFirstMiss));
        //Once the car is confirmed away the presence is certain again: the car is not at home.
        service.RegisterOutOfRange(CarId, Start + Threshold);
        Assert.False(service.IsPresenceUncertain(CarId, OnFirstMiss));
    }

    /// <summary>
    /// The point of the tolerance: a weak radio misses scans on a car that is provably at home, and every miss used to
    /// block charging control until the next hit. Isolated misses must not suspend commands anymore.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void PresenceStaysCertainUntilTheConfiguredMissCountIsReached(int missesBeforeUncertain)
    {
        var service = NewService();
        for (var miss = 1; miss < missesBeforeUncertain; miss++)
        {
            service.RegisterOutOfRange(CarId, Start.AddSeconds(miss));
            Assert.False(service.IsPresenceUncertain(CarId, missesBeforeUncertain),
                $"{miss} miss(es) must not suspend charging commands when {missesBeforeUncertain} are tolerated");
        }
        service.RegisterOutOfRange(CarId, Start.AddSeconds(missesBeforeUncertain));
        Assert.True(service.IsPresenceUncertain(CarId, missesBeforeUncertain));
    }

    /// <summary>
    /// A single hit between misses is what a flaky link produces, and it has to fully clear the streak - otherwise the
    /// tolerance would be used up after the first few scans and never recover.
    /// </summary>
    [Fact]
    public void HitBetweenMissesClearsTheToleranceAgain()
    {
        const int tolerance = 2;
        var service = NewService();
        service.RegisterOutOfRange(CarId, Start);
        service.RegisterSuccessfulRead(CarId);
        service.RegisterOutOfRange(CarId, Start.AddSeconds(26));
        Assert.False(service.IsPresenceUncertain(CarId, tolerance));
        service.RegisterOutOfRange(CarId, Start.AddSeconds(39));
        Assert.True(service.IsPresenceUncertain(CarId, tolerance));
    }

    /// <summary>
    /// Raising the tolerance must not slow down detecting a car that really left; that has its own confirmation
    /// duration and is what actually stops charging in the away case.
    /// </summary>
    [Fact]
    public void ToleranceDoesNotDelayAwayConfirmation()
    {
        var service = NewService();
        Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(CarId, Start));
        Assert.Equal(BleAwayConfirmation.JustConfirmed, service.RegisterOutOfRange(CarId, Start + Threshold));
        //Confirmed away is not "uncertain" at any tolerance: IsHome is false and stops charging on its own.
        Assert.False(service.IsPresenceUncertain(CarId, 1));
        Assert.False(service.IsPresenceUncertain(CarId, 50));
    }

    /// <summary>
    /// The streak counter is capped, so the cap must stay above the largest configurable tolerance or a high setting
    /// could never be reached and commands would never be suspended.
    /// </summary>
    [Fact]
    public void CounterReachesTheLargestConfigurableTolerance()
    {
        const int largestConfigurable = 100;
        var service = NewService();
        for (var miss = 0; miss < largestConfigurable; miss++)
        {
            //Deliberately inside the away confirmation window so only the miss count can make presence uncertain.
            service.RegisterOutOfRange(CarId, Start.AddMilliseconds(miss));
        }
        Assert.True(service.IsPresenceUncertain(CarId, largestConfigurable));
    }

    [Fact]
    public void SuccessfulReadResetsTheStreak()
    {
        var service = NewService();
        service.RegisterOutOfRange(CarId, Start);
        service.RegisterOutOfRange(CarId, Start + Threshold - TimeSpan.FromSeconds(1));
        service.RegisterSuccessfulRead(CarId);
        Assert.False(service.IsPresenceUncertain(CarId, OnFirstMiss));
        //After a successful read the full confirmation duration is required again, measured from the next miss.
        var restart = Start + Threshold;
        Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(CarId, restart));
        Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(CarId, restart + Threshold - TimeSpan.FromSeconds(1)));
        Assert.Equal(BleAwayConfirmation.JustConfirmed, service.RegisterOutOfRange(CarId, restart + Threshold));
    }

    [Fact]
    public void ResetClearsTheState()
    {
        var service = NewService();
        service.RegisterOutOfRange(CarId, Start);
        Assert.True(service.IsPresenceUncertain(CarId, OnFirstMiss));
        service.Reset(CarId);
        Assert.False(service.IsPresenceUncertain(CarId, OnFirstMiss));
    }

    [Fact]
    public void RetainOnlyDropsStateOfCarsNoLongerBlePolled()
    {
        var service = NewService();
        service.RegisterOutOfRange(CarId, Start);
        service.RegisterOutOfRange(OtherCarId, Start);
        service.RetainOnly(new[] { OtherCarId });
        //The dropped car must not keep a stale uncertain state that would suppress its charging commands.
        Assert.False(service.IsPresenceUncertain(CarId, OnFirstMiss));
        Assert.True(service.IsPresenceUncertain(OtherCarId, OnFirstMiss));
        service.RetainOnly(Array.Empty<int>());
        Assert.False(service.IsPresenceUncertain(OtherCarId, OnFirstMiss));
    }

    [Fact]
    public void CarsAreTrackedIndependently()
    {
        var service = NewService();
        service.RegisterOutOfRange(CarId, Start);
        service.RegisterOutOfRange(CarId, Start + Threshold);
        Assert.False(service.IsPresenceUncertain(CarId, OnFirstMiss));
        //The other car's streak only starts now, so it is nowhere near confirmation.
        Assert.Equal(BleAwayConfirmation.NotConfirmed, service.RegisterOutOfRange(OtherCarId, Start + Threshold));
        Assert.True(service.IsPresenceUncertain(OtherCarId, OnFirstMiss));
    }

    [Fact]
    public void RadioSilenceIsMeasuredFromTheLastHeardAdvertisement()
    {
        var service = NewService();
        const string container = "http://raspible:7210/|";
        var start = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        //The first registration seeds the timestamp so a freshly started server never reports a silence longer than
        //its own observation window.
        Assert.Equal(TimeSpan.Zero, service.RegisterScanEvidence(container, heardAnything: true, start));
        Assert.Equal(TimeSpan.FromHours(2), service.RegisterScanEvidence(container, heardAnything: false, start.AddHours(2)));
        //Any received advertisement proves the radio works and restarts the measurement.
        Assert.Equal(TimeSpan.Zero, service.RegisterScanEvidence(container, heardAnything: true, start.AddHours(3)));
        Assert.Equal(TimeSpan.FromHours(1), service.RegisterScanEvidence(container, heardAnything: false, start.AddHours(4)));
    }

    [Fact]
    public void RadioSilenceStartsCountingOnTheFirstSilentScan()
    {
        var service = NewService();
        const string container = "http://raspible:7210/|AA:BB:CC:DD:EE:FF";
        var start = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, service.RegisterScanEvidence(container, heardAnything: false, start));
        Assert.Equal(TimeSpan.FromHours(25), service.RegisterScanEvidence(container, heardAnything: false, start.AddHours(25)));
    }
}
