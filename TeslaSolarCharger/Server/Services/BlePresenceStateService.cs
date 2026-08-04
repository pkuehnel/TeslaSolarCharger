using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class BlePresenceStateService(ILogger<BlePresenceStateService> logger) : IBlePresenceStateService
{
    //A single out of range result can be a transient BLE stack failure while the car is at home, and such failures can
    //occur multiple times in a row, so a car counts as away only after being unreachable for this long without
    //interruption. Deliberately a duration and not a number of polls: the poll interval is configurable
    //(BleDataRefreshIntervalSeconds), so a fixed count would silently change how long a car has to be gone.
    internal static readonly TimeSpan AwayConfirmationDuration = TimeSpan.FromMinutes(2.5);

    //Guards the degenerate case of a poller that stalled for longer than the confirmation duration: on its first poll
    //after resuming, the elapsed time alone would confirm a car as away that was only observed as missing once.
    internal const int MinimumMissesToConfirmAway = 2;

    //The streak counter is capped purely so a car parked elsewhere for weeks cannot grow it without bound. It has to
    //stay well above the largest configurable BleMissesBeforePresenceUncertain (100), otherwise a high threshold could
    //never be reached and presence would never count as uncertain.
    internal const int MaxTrackedMisses = 1000;

    //Bounded by count and by age: the poll interval is configurable, so a count alone would cover minutes on a fast
    //interval and hours on a slow one. Whichever limit bites first wins.
    internal const int MaxObservationsPerCar = 200;
    internal static readonly TimeSpan ObservationRetention = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<int, MissStreak> _outOfRangeStreaks = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRadioEvidence = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, BeaconObservationHistory> _observations = new();

    /// <summary>
    /// An uninterrupted run of out of range results: when it started, how many were seen and whether the away
    /// transition already ran.
    /// </summary>
    private sealed record MissStreak(DateTime FirstMissUtc, int MissCount, bool Confirmed);

    public void RegisterSuccessfulRead(int carId)
    {
        if (_outOfRangeStreaks.TryRemove(carId, out var previousStreak) && previousStreak.MissCount > 0)
        {
            logger.LogDebug("Car {carId} answered via BLE after {count} out of range results, car is in range", carId,
                previousStreak.MissCount);
        }
    }

    public BleAwayConfirmation RegisterOutOfRange(int carId, DateTime nowUtc)
    {
        var streak = _outOfRangeStreaks.AddOrUpdate(carId,
            _ => new MissStreak(nowUtc, 1, false),
            (_, existing) => existing with
            {
                //The timestamp of the first miss is never moved, as that is what the confirmation duration is measured
                //from.
                MissCount = Math.Min(existing.MissCount + 1, MaxTrackedMisses),
            });
        if (streak.Confirmed)
        {
            return BleAwayConfirmation.AlreadyConfirmed;
        }
        var unreachableFor = nowUtc - streak.FirstMissUtc;
        if (unreachableFor < AwayConfirmationDuration || streak.MissCount < MinimumMissesToConfirmAway)
        {
            logger.LogDebug("Car {carId} out of BLE range for {unreachableFor} ({count} results), threshold is {threshold}, keeping last known state",
                carId, unreachableFor, streak.MissCount, AwayConfirmationDuration);
            return BleAwayConfirmation.NotConfirmed;
        }
        //Only the poll that flips Confirmed reports JustConfirmed, so the caller runs the away transition exactly once.
        var justConfirmed = false;
        _outOfRangeStreaks.AddOrUpdate(carId,
            _ => new MissStreak(nowUtc, streak.MissCount, true),
            (_, existing) =>
            {
                if (existing.Confirmed)
                {
                    return existing;
                }
                justConfirmed = true;
                return existing with { Confirmed = true };
            });
        return justConfirmed ? BleAwayConfirmation.JustConfirmed : BleAwayConfirmation.AlreadyConfirmed;
    }

    public bool IsPresenceUncertain(int carId, int missesBeforeUncertain)
    {
        if (!_outOfRangeStreaks.TryGetValue(carId, out var streak) || streak.Confirmed)
        {
            //Not tracked (last scan found it) or already confirmed away, which sets IsHome false and stops charging on
            //its own. Neither case is the "might have left" limbo this reports.
            return false;
        }
        //A weak radio misses scans on a car that is provably at home, so tolerate a configurable number of them before
        //blocking charging commands. Away detection is unaffected: it still needs its own confirmation duration.
        return streak.MissCount >= (missesBeforeUncertain < 1 ? 1 : missesBeforeUncertain);
    }

    public void Reset(int carId)
    {
        //The observation history is deliberately kept: it is diagnostic only, and it is most useful exactly when a car
        //was just reset because it looked away.
        if (_outOfRangeStreaks.TryRemove(carId, out _))
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
        foreach (var trackedCarId in _outOfRangeStreaks.Keys)
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

    public TimeSpan RegisterScanEvidence(string containerKey, bool heardAnything, DateTimeOffset timestamp)
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
    /// One car's beacon observations. Locked rather than lock free: the BLE refresh appends from the poll job while
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
                var found = observations.Where(o => o.BeaconFound).ToList();
                return new DtoBleBeaconHistory
                {
                    Observations = observations,
                    TotalScans = observations.Count,
                    FoundScans = found.Count,
                    HitRatePercent = observations.Count == 0
                        ? null
                        : Math.Round(found.Count * 100d / observations.Count, 1),
                    AverageRssi = found.Any(o => o.Rssi != null)
                        ? Math.Round(found.Where(o => o.Rssi != null).Average(o => o.Rssi!.Value), 1)
                        : null,
                    LongestMissStreak = LongestMissStreak(observations),
                    LastFoundAt = found.LastOrDefault()?.Timestamp,
                };
            }
        }

        private static int LongestMissStreak(List<DtoBleBeaconObservation> observations)
        {
            var longest = 0;
            var current = 0;
            foreach (var observation in observations)
            {
                current = observation.BeaconFound ? 0 : current + 1;
                longest = Math.Max(longest, current);
            }
            return longest;
        }
    }
}
