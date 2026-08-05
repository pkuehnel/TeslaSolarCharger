using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

/// <summary>
/// Decides what the age of the newest evidence about a car means.
///
/// The container answers "how long ago was this car last heard", counting both its advertisements and the commands it
/// answered. A Tesla emits nothing at all while it holds a connection to us, so those two sources are complementary
/// and only both fall silent when the car really is gone. There is no sampling any more: the old miss streak
/// machinery existed to smooth a scan that looked once every 13 s, and the age already carries that history.
/// </summary>
public class BlePresenceStateService(ILogger<BlePresenceStateService> logger) : IBlePresenceStateService
{
    /// <summary>
    /// How long a car may be over the max age before it counts as away. Deliberately on top of the max age rather
    /// than replacing it: the max age answers "is the car here right now", this answers "has it been gone long
    /// enough to act on". Together they put the away transition at about four minutes of true silence.
    /// </summary>
    internal static readonly TimeSpan AwayConfirmationDuration = TimeSpan.FromMinutes(2.5);

    //Bounded by count and by age: the poll interval is configurable, so a count alone would cover minutes on a fast
    //interval and hours on a slow one. Whichever limit bites first wins.
    internal const int MaxObservationsPerCar = 200;
    internal static readonly TimeSpan ObservationRetention = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<int, CarState> _states = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRadioEvidence = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, BeaconObservationHistory> _observations = new();

    /// <summary>The last decision for a car, and whether the away transition has already run.</summary>
    private sealed record CarState(BlePresenceDecision Decision, bool AwayHandled);

    public BlePresenceDecision RegisterPresenceAge(int carId, TimeSpan? age, TimeSpan maxAge)
    {
        if (age is not { } evidenceAge)
        {
            //Never heard, or the container cannot say. Not the same as "not there".
            _states[carId] = new CarState(BlePresenceDecision.Unknown, AwayHandled(carId));
            return BlePresenceDecision.Unknown;
        }
        if (evidenceAge <= maxAge)
        {
            if (AwayHandled(carId))
            {
                logger.LogDebug("Car {carId} was heard again after being away", carId);
            }
            _states[carId] = new CarState(BlePresenceDecision.Present, false);
            return BlePresenceDecision.Present;
        }
        if (evidenceAge <= maxAge + AwayConfirmationDuration)
        {
            logger.LogDebug("Car {carId} not heard for {age}, threshold is {maxAge}, keeping last known state",
                carId, evidenceAge, maxAge);
            _states[carId] = new CarState(BlePresenceDecision.Uncertain, false);
            return BlePresenceDecision.Uncertain;
        }
        //Only the first decision past the confirmation reports JustConfirmedAway, so the caller runs the away
        //transition exactly once.
        var justConfirmed = !AwayHandled(carId);
        _states[carId] = new CarState(BlePresenceDecision.AlreadyAway, true);
        if (justConfirmed)
        {
            logger.LogInformation("Car {carId} has not been heard for {age}, confirming it as away", carId, evidenceAge);
        }
        return justConfirmed ? BlePresenceDecision.JustConfirmedAway : BlePresenceDecision.AlreadyAway;
    }

    private bool AwayHandled(int carId) => _states.TryGetValue(carId, out var state) && state.AwayHandled;

    public bool IsPresenceUncertain(int carId) =>
        _states.TryGetValue(carId, out var state) && state.Decision == BlePresenceDecision.Uncertain;

    public void Reset(int carId)
    {
        //The observation history is deliberately kept: it is diagnostic only, and it is most useful exactly when a
        //car was just reset because it looked away.
        if (_states.TryRemove(carId, out _))
        {
            logger.LogDebug("Reset BLE presence state of car {carId}", carId);
        }
    }

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
        foreach (var trackedCarId in _states.Keys)
        {
            if (!carIds.Contains(trackedCarId))
            {
                Reset(trackedCarId);
            }
        }
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
