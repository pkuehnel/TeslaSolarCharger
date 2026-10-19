using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;

namespace TeslaSolarCharger.Server.Services;

/// <summary>
/// In memory diagnostics of the BLE presence: when each radio last heard anything and the recent presence
/// observations of each car. Presence itself is decided in <see cref="BleVehicleDataService.IsPresent"/>.
/// </summary>
public class BlePresenceStateService : IBlePresenceStateService
{
    //Bounded by count and by age: the poll interval is configurable, so a count alone would cover minutes on a fast
    //interval and hours on a slow one. Whichever limit bites first wins.
    internal const int MaxObservationsPerCar = 200;
    internal static readonly TimeSpan ObservationRetention = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRadioEvidence = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, BeaconObservationHistory> _observations = new();

    public void RegisterObservation(int carId, DtoBleBeaconObservation observation)
    {
        var history = _observations.GetOrAdd(carId, _ => new BeaconObservationHistory());
        history.Add(observation);
    }

    public DtoBleBeaconHistory GetObservations(int carId)
    {
        return _observations.TryGetValue(carId, out var history)
            ? history.Snapshot()
            : new DtoBleBeaconHistory();
    }

    public void RetainOnly(IReadOnlyCollection<int> carIds)
    {
        //A car that left BLE data collection will never get another observation, so its history would just sit there.
        foreach (var trackedCarId in _observations.Keys)
        {
            if (!carIds.Contains(trackedCarId))
            {
                _observations.TryRemove(trackedCarId, out _);
            }
        }
    }

    public TimeSpan RegisterRadioEvidence(string containerKey, bool heardAnything, DateTimeOffset timestamp)
    {
        if (heardAnything)
        {
            _lastRadioEvidence[containerKey] = timestamp;
            return TimeSpan.Zero;
        }
        //Seed on the first ever registration so a freshly started server never reports a silence longer than its own
        //observation window.
        var lastEvidence = _lastRadioEvidence.GetOrAdd(containerKey, timestamp);
        return timestamp - lastEvidence;
    }

    /// <summary>
    /// One car's presence observations. Locked rather than lock free: the BLE refresh appends from the poll job while
    /// the support page reads, and a plain queue would tear under that.
    /// </summary>
    private sealed class BeaconObservationHistory
    {
        private readonly object _lock = new();
        private readonly LinkedList<DtoBleBeaconObservation> _observations = new();

        public void Add(DtoBleBeaconObservation observation)
        {
            lock (_lock)
            {
                _observations.AddLast(observation);
                //Age is evaluated against the newest sample rather than the wall clock, so the history stays testable
                //without a clock and a stalled poller cannot silently empty it.
                var cutoff = observation.Timestamp - ObservationRetention;
                while (_observations.Count > MaxObservationsPerCar
                       || (_observations.First != null && _observations.First.Value.Timestamp < cutoff))
                {
                    _observations.RemoveFirst();
                }
            }
        }

        public DtoBleBeaconHistory Snapshot()
        {
            lock (_lock)
            {
                var observations = _observations.ToList();
                var present = observations.Where(o => o.IsPresent).ToList();
                return new DtoBleBeaconHistory
                {
                    Observations = observations,
                    TotalScans = observations.Count,
                    FoundScans = present.Count,
                    HitRatePercent = observations.Count == 0
                        ? null
                        : Math.Round(present.Count * 100d / observations.Count, 1),
                    AverageRssi = present.Any(o => o.Rssi != null)
                        ? Math.Round(present.Where(o => o.Rssi != null).Average(o => o.Rssi!.Value), 1)
                        : null,
                    LongestMissStreak = LongestMissStreak(observations),
                    LastFoundAt = present.LastOrDefault()?.Timestamp,
                };
            }
        }

        private static int LongestMissStreak(List<DtoBleBeaconObservation> observations)
        {
            var longest = 0;
            var current = 0;
            foreach (var observation in observations)
            {
                current = observation.IsPresent ? 0 : current + 1;
                longest = Math.Max(longest, current);
            }
            return longest;
        }
    }
}
