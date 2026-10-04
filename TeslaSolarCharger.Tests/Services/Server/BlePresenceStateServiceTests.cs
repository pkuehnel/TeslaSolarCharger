using System;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BlePresenceStateServiceTests
{
    private const int CarId = 1;
    private const int OtherCarId = 2;

    private static IBlePresenceStateService NewService() => new BlePresenceStateService();

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
