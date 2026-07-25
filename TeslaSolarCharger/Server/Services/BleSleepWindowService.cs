using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class BleSleepWindowService(ILogger<BleSleepWindowService> logger) : IBleSleepWindowService
{
    private const string ClosureStateClosed = "CLOSURESTATE_CLOSED";
    private const string UserPresencePresent = "VEHICLE_USER_PRESENCE_PRESENT";

    private readonly ConcurrentDictionary<int, SleepWindowState> _states = new();

    public bool ShouldPollInfotainment(int carId, DateTime nowUtc, int windowMinutes)
    {
        //Feature disabled: always poll the infotainment system like before.
        if (windowMinutes <= 0)
        {
            return true;
        }
        if (!_states.TryGetValue(carId, out var state))
        {
            //Not tracked yet (car just woke/arrived): poll so a full poll can establish the stability baseline.
            return true;
        }
        lock (state)
        {
            if (state.WindowStartUtc is not { } windowStart)
            {
                //In the stability phase: keep doing full polls.
                return true;
            }
            if ((nowUtc - windowStart).TotalMinutes >= windowMinutes)
            {
                //Window elapsed: end it and do one fresh full poll. ObserveFullPoll re-enters a window if still idle.
                logger.LogDebug("BLE sleep window for car {carId} elapsed, do a single infotainment poll", carId);
                state.WindowStartUtc = null;
                return true;
            }
            //Inside an active window: withhold the infotainment poll so the car can fall asleep.
            return false;
        }
    }

    public void ObserveFullPoll(int carId, DtoBleBodyControllerState bodyControllerState, bool? pluggedIn,
        int? chargeLimitSoc, DateTime nowUtc, int windowMinutes, int stabilityMinutes)
    {
        if (windowMinutes <= 0)
        {
            //Feature disabled: do not keep any state so nothing silences the polling and the UI shows no window.
            _states.TryRemove(carId, out _);
            return;
        }
        var state = _states.GetOrAdd(carId, _ => new SleepWindowState { StabilitySinceUtc = nowUtc });
        lock (state)
        {
            if (state.IsAsleep)
            {
                //The car just woke up again: treat it like a fresh arrival and start a new stability period.
                state.IsAsleep = false;
                state.WindowStartUtc = null;
                state.StabilitySinceUtc = nowUtc;
                state.LastSignature = null;
            }
            var signature = BuildSignature(bodyControllerState, pluggedIn, chargeLimitSoc);
            if (state.LastSignature != null && !string.Equals(state.LastSignature, signature, StringComparison.Ordinal))
            {
                //A tracked value (door/frunk/trunk state, plugged in state, charge limit or occupant) changed: this
                //counts as activity, so restart the stability period.
                logger.LogDebug("BLE relevant state of car {carId} changed, restart sleep stability period", carId);
                state.StabilitySinceUtc = nowUtc;
            }
            state.LastSignature = signature;
            //Remembered so the UI can tell that no sleep window can start and whether the user may start one manually.
            state.ClosedAndEmpty = AllCountedClosuresClosed(bodyControllerState) && !IsOccupantPresent(bodyControllerState);

            //ObserveFullPoll only runs right after ShouldPollInfotainment returned true, so any previous window is
            //already ended (WindowStartUtc is null here). Only (re-)enter a window, never keep a stale one.
            if (state.ClosedAndEmpty == true
                && (nowUtc - state.StabilitySinceUtc).TotalMinutes >= stabilityMinutes)
            {
                logger.LogDebug("Car {carId} stable for {stabilityMinutes} min, start BLE sleep window", carId, stabilityMinutes);
                state.WindowStartUtc = nowUtc;
            }
        }
    }

    public void NotifyAsleep(int carId)
    {
        //Keep (or create) the entry so the UI can show the asleep phase. Marking it asleep clears any active window;
        //the stability period restarts on the next awake full poll (see ObserveFullPoll).
        var state = _states.GetOrAdd(carId, _ => new SleepWindowState());
        lock (state)
        {
            state.IsAsleep = true;
            state.WindowStartUtc = null;
        }
    }

    public void ResetSleepWindow(int carId)
    {
        if (_states.TryRemove(carId, out _))
        {
            logger.LogDebug("Reset BLE sleep window state of car {carId}", carId);
        }
    }

    public DtoBleSleepWindowStatus? GetStatus(int carId, DateTime nowUtc, int windowMinutes, int stabilityMinutes)
    {
        if (windowMinutes <= 0)
        {
            return null;
        }
        if (!_states.TryGetValue(carId, out var state))
        {
            return null;
        }
        lock (state)
        {
            if (state.IsAsleep)
            {
                return new DtoBleSleepWindowStatus { Phase = BleSleepPhase.Asleep, SecondsRemaining = null };
            }
            if (state.WindowStartUtc is { } windowStart)
            {
                var remaining = windowMinutes * 60 - (int)(nowUtc - windowStart).TotalSeconds;
                return new DtoBleSleepWindowStatus
                {
                    Phase = BleSleepPhase.TryingToSleep,
                    SecondsRemaining = Math.Max(0, remaining),
                };
            }
            var stabilityRemaining = stabilityMinutes * 60 - (int)(nowUtc - state.StabilitySinceUtc).TotalSeconds;
            return new DtoBleSleepWindowStatus
            {
                Phase = BleSleepPhase.WaitingToSleep,
                SecondsRemaining = Math.Max(0, stabilityRemaining),
                CarClosedAndEmpty = state.ClosedAndEmpty,
            };
        }
    }

    public bool TryStartWindowNow(int carId, DateTime nowUtc, int windowMinutes)
    {
        if (windowMinutes <= 0)
        {
            return false;
        }
        if (!_states.TryGetValue(carId, out var state))
        {
            //Nothing observed yet, so it is unknown whether the car is closed up: do not silence it.
            return false;
        }
        lock (state)
        {
            if (!CanStartWindow(state))
            {
                return false;
            }
            logger.LogDebug("Manually start BLE sleep window for car {carId}", carId);
            state.WindowStartUtc = nowUtc;
            return true;
        }
    }

    /// <summary>
    /// Whether a sleep window may be started right now, ignoring the stability period: that one only exists to avoid
    /// silencing a car that is still in use, which the user overrules by starting a window manually.
    /// </summary>
    private static bool CanStartWindow(SleepWindowState state) =>
        !state.IsAsleep
        && state.WindowStartUtc == null
        && state.ClosedAndEmpty == true;

    private static string BuildSignature(DtoBleBodyControllerState bcs, bool? pluggedIn, int? chargeLimitSoc)
    {
        var c = bcs.ClosureStatuses;
        return string.Join("|",
            c?.FrontDriverDoor ?? "null",
            c?.FrontPassengerDoor ?? "null",
            c?.RearDriverDoor ?? "null",
            c?.RearPassengerDoor ?? "null",
            c?.FrontTrunk ?? "null",
            c?.RearTrunk ?? "null",
            bcs.UserPresence ?? "null",
            pluggedIn?.ToString() ?? "null",
            chargeLimitSoc?.ToString() ?? "null");
    }

    /// <summary>
    /// True if all counted closures (four doors, front trunk and rear trunk) are reported closed. The charge port door
    /// and tonneau are intentionally ignored. A missing closure value counts as not closed (conservative: do not
    /// silence unless we are sure the car is closed up).
    /// </summary>
    private static bool AllCountedClosuresClosed(DtoBleBodyControllerState bcs)
    {
        var c = bcs.ClosureStatuses;
        if (c == null)
        {
            return false;
        }
        return IsClosed(c.FrontDriverDoor)
               && IsClosed(c.FrontPassengerDoor)
               && IsClosed(c.RearDriverDoor)
               && IsClosed(c.RearPassengerDoor)
               && IsClosed(c.FrontTrunk)
               && IsClosed(c.RearTrunk);
    }

    private static bool IsClosed(string? closureState)
    {
        return string.Equals(closureState, ClosureStateClosed, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOccupantPresent(DtoBleBodyControllerState bcs)
    {
        return string.Equals(bcs.UserPresence, UserPresencePresent, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SleepWindowState
    {
        public DateTime StabilitySinceUtc { get; set; }
        public string? LastSignature { get; set; }
        public DateTime? WindowStartUtc { get; set; }
        public bool IsAsleep { get; set; }
        //All counted closures closed and nobody in the car. Null until a full poll was observed: unknown, which counts
        //as "not ready" for starting a window.
        public bool? ClosedAndEmpty { get; set; }
    }
}
