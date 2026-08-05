using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// Presence is decided on the age of the newest evidence about a car, which the container computes from both its
/// advertisements and the commands it answered. There is no sampling any more: the miss streak machinery this
/// replaced existed to smooth a scan that looked once every 13 s.
/// </summary>
public class BlePresenceStateServiceTests
{
    private const int CarId = 1;
    private const int OtherCarId = 2;
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan Confirmation = BlePresenceStateService.AwayConfirmationDuration;

    private static IBlePresenceStateService NewService()
        => new BlePresenceStateService(Mock.Of<ILogger<BlePresenceStateService>>());

    [Fact]
    public void ACarHeardWithinTheMaxAgeIsPresent()
    {
        var service = NewService();
        Assert.Equal(BlePresenceDecision.Present, service.RegisterPresenceAge(CarId, TimeSpan.FromSeconds(20), MaxAge));
        Assert.False(service.IsPresenceUncertain(CarId));
    }

    [Fact]
    public void ACarOverTheMaxAgeIsUncertainButKeepsItsState()
    {
        var service = NewService();
        var decision = service.RegisterPresenceAge(CarId, MaxAge + TimeSpan.FromSeconds(1), MaxAge);

        Assert.Equal(BlePresenceDecision.Uncertain, decision);
        //Charging commands are suspended, but nothing about the car's state is written yet.
        Assert.True(service.IsPresenceUncertain(CarId));
    }

    [Fact]
    public void AwayIsConfirmedExactlyOnceAfterTheConfirmationDuration()
    {
        var service = NewService();
        service.RegisterPresenceAge(CarId, MaxAge + TimeSpan.FromSeconds(1), MaxAge);

        var justConfirmed = service.RegisterPresenceAge(CarId, MaxAge + Confirmation + TimeSpan.FromSeconds(1), MaxAge);
        var again = service.RegisterPresenceAge(CarId, MaxAge + Confirmation + TimeSpan.FromMinutes(5), MaxAge);

        //Only the first one may run the away transition, otherwise the charging values would be rewritten every poll.
        Assert.Equal(BlePresenceDecision.JustConfirmedAway, justConfirmed);
        Assert.Equal(BlePresenceDecision.AlreadyAway, again);
        //A confirmed away car sets IsHome false on its own, so it is not the "might have left" limbo.
        Assert.False(service.IsPresenceUncertain(CarId));
    }

    [Fact]
    public void HearingTheCarAgainClearsTheAwayStateSoItCanBeConfirmedOnceMore()
    {
        var service = NewService();
        service.RegisterPresenceAge(CarId, MaxAge + Confirmation + TimeSpan.FromSeconds(1), MaxAge);
        Assert.Equal(BlePresenceDecision.Present, service.RegisterPresenceAge(CarId, TimeSpan.Zero, MaxAge));

        Assert.Equal(BlePresenceDecision.JustConfirmedAway,
            service.RegisterPresenceAge(CarId, MaxAge + Confirmation + TimeSpan.FromSeconds(1), MaxAge));
    }

    /// <summary>
    /// The container says nothing while its scan is warming up after a restart. Concluding "away" from that would
    /// mark every car as gone whenever the container or its worker restarts.
    /// </summary>
    [Fact]
    public void AnUnknownAgeConcludesNothing()
    {
        var service = NewService();
        service.RegisterPresenceAge(CarId, TimeSpan.FromSeconds(20), MaxAge);

        Assert.Equal(BlePresenceDecision.Unknown, service.RegisterPresenceAge(CarId, null, MaxAge));
        Assert.False(service.IsPresenceUncertain(CarId));
    }

    [Fact]
    public void AnUnknownAgeDoesNotUndoAConfirmedAway()
    {
        var service = NewService();
        service.RegisterPresenceAge(CarId, MaxAge + Confirmation + TimeSpan.FromSeconds(1), MaxAge);
        service.RegisterPresenceAge(CarId, null, MaxAge);

        //Still away, so the transition must not run a second time when the container can answer again.
        Assert.Equal(BlePresenceDecision.AlreadyAway,
            service.RegisterPresenceAge(CarId, MaxAge + Confirmation + TimeSpan.FromSeconds(2), MaxAge));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(600)]
    public void TheMaxAgeIsWhatDecides(int maxAgeSeconds)
    {
        var service = NewService();
        var maxAge = TimeSpan.FromSeconds(maxAgeSeconds);

        Assert.Equal(BlePresenceDecision.Present, service.RegisterPresenceAge(CarId, maxAge, maxAge));
        Assert.Equal(BlePresenceDecision.Uncertain,
            service.RegisterPresenceAge(CarId, maxAge + TimeSpan.FromMilliseconds(1), maxAge));
    }

    [Fact]
    public void CarsAreTrackedIndependently()
    {
        var service = NewService();
        service.RegisterPresenceAge(CarId, MaxAge + TimeSpan.FromSeconds(1), MaxAge);
        service.RegisterPresenceAge(OtherCarId, TimeSpan.Zero, MaxAge);

        Assert.True(service.IsPresenceUncertain(CarId));
        Assert.False(service.IsPresenceUncertain(OtherCarId));
    }

    [Fact]
    public void ResetClearsTheState()
    {
        var service = NewService();
        service.RegisterPresenceAge(CarId, MaxAge + TimeSpan.FromSeconds(1), MaxAge);
        service.Reset(CarId);
        Assert.False(service.IsPresenceUncertain(CarId));
    }

    [Fact]
    public void RetainOnlyDropsStateOfCarsNoLongerBlePolled()
    {
        var service = NewService();
        service.RegisterPresenceAge(CarId, MaxAge + TimeSpan.FromSeconds(1), MaxAge);
        service.RegisterPresenceAge(OtherCarId, MaxAge + TimeSpan.FromSeconds(1), MaxAge);

        service.RetainOnly(new[] { OtherCarId });

        //A car that left BLE data collection must not keep a stale uncertain state suppressing its commands forever.
        Assert.False(service.IsPresenceUncertain(CarId));
        Assert.True(service.IsPresenceUncertain(OtherCarId));
    }

    private static DtoBleBeaconObservation Observation(DateTimeOffset timestamp, bool isPresent, int? rssi = null,
        string? source = null) => new()
    {
        Timestamp = timestamp,
        IsPresent = isPresent,
        Rssi = rssi,
        EvidenceSource = source,
        LastSeenMsAgo = isPresent ? 100 : 120000,
    };

    [Fact]
    public void ObservationsAreRecordedWithTheirSummary()
    {
        var service = NewService();
        var start = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        service.RegisterObservation(CarId, Observation(start, true, -60, "advertisement"));
        service.RegisterObservation(CarId, Observation(start.AddSeconds(13), false));
        service.RegisterObservation(CarId, Observation(start.AddSeconds(26), false));
        service.RegisterObservation(CarId, Observation(start.AddSeconds(39), true, -70, "command"));

        var history = service.GetObservations(CarId);

        Assert.Equal(4, history.TotalScans);
        Assert.Equal(2, history.FoundScans);
        Assert.Equal(50d, history.HitRatePercent);
        Assert.Equal(-65d, history.AverageRssi);
        Assert.Equal(2, history.LongestMissStreak);
        Assert.Equal(start.AddSeconds(39), history.LastFoundAt);
        Assert.Equal("command", history.Observations.Last().EvidenceSource);
    }

    [Fact]
    public void ObservationsOfAnUnknownCarAreEmptyRatherThanNull()
    {
        var history = NewService().GetObservations(CarId);
        Assert.Empty(history.Observations);
        Assert.Null(history.HitRatePercent);
    }

    [Fact]
    public void ObservationsAreCappedByCount()
    {
        var service = NewService();
        var start = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < BlePresenceStateService.MaxObservationsPerCar + 50; index++)
        {
            service.RegisterObservation(CarId, Observation(start.AddSeconds(index), true));
        }
        Assert.Equal(BlePresenceStateService.MaxObservationsPerCar, service.GetObservations(CarId).TotalScans);
    }

    [Fact]
    public void ObservationsAreCappedByAge()
    {
        var service = NewService();
        var start = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        service.RegisterObservation(CarId, Observation(start, true));
        //Age is measured against the newest sample, so a stalled poller cannot silently empty the history.
        service.RegisterObservation(CarId, Observation(start.Add(BlePresenceStateService.ObservationRetention).AddMinutes(1), true));

        Assert.Equal(1, service.GetObservations(CarId).TotalScans);
    }

    [Fact]
    public void RetainOnlyDropsObservationsOfCarsNoLongerBlePolled()
    {
        var service = NewService();
        var start = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        service.RegisterObservation(CarId, Observation(start, true));
        service.RegisterObservation(OtherCarId, Observation(start, true));

        service.RetainOnly(new[] { OtherCarId });

        Assert.Empty(service.GetObservations(CarId).Observations);
        Assert.Single(service.GetObservations(OtherCarId).Observations);
    }

    [Fact]
    public async Task ConcurrentAppendsAndReadsDoNotThrow()
    {
        var service = NewService();
        var start = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        //The refresh job appends while the support page reads; a plain queue would tear under that.
        var writer = Task.Run(() =>
        {
            for (var index = 0; index < 500; index++)
            {
                service.RegisterObservation(CarId, Observation(start.AddSeconds(index), index % 2 == 0));
            }
        });
        var reader = Task.Run(() =>
        {
            for (var index = 0; index < 500; index++)
            {
                _ = service.GetObservations(CarId).TotalScans;
            }
        });
        await Task.WhenAll(writer, reader);
    }

    [Fact]
    public void RadioSilenceIsMeasuredFromTheLastHeardAdvertisement()
    {
        var service = NewService();
        const string container = "http://raspible:7210/|AA:BB:CC:DD:EE:FF";
        var start = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, service.RegisterRadioEvidence(container, heardAnything: true, start));
        Assert.Equal(TimeSpan.FromHours(3),
            service.RegisterRadioEvidence(container, heardAnything: false, start.AddHours(3)));
        Assert.Equal(TimeSpan.Zero,
            service.RegisterRadioEvidence(container, heardAnything: true, start.AddHours(4)));
    }

    [Fact]
    public void RadioSilenceStartsCountingOnTheFirstSilentPoll()
    {
        var service = NewService();
        const string container = "http://raspible:7210/|AA:BB:CC:DD:EE:FF";
        var start = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        //Seeded on the first registration, so a freshly started server never reports more silence than it observed.
        Assert.Equal(TimeSpan.Zero, service.RegisterRadioEvidence(container, heardAnything: false, start));
        Assert.Equal(TimeSpan.FromHours(25), service.RegisterRadioEvidence(container, heardAnything: false, start.AddHours(25)));
    }
}
