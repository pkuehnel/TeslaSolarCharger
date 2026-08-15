using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;
using ClosureStatuses = VCSEC.ClosureStatuses;
using ClosureState = VCSEC.ClosureState_E;
using UserPresence = VCSEC.UserPresence_E;
using VehicleStatus = VCSEC.VehicleStatus;

namespace TeslaSolarCharger.Server.Services;

public class BleSleepWindowService(ILogger<BleSleepWindowService> logger) : IBleSleepWindowService
{
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

    public void ObserveFullPoll(int carId, VehicleStatus bodyControllerState, bool? pluggedIn,
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

    private static string BuildSignature(VehicleStatus bcs, bool? pluggedIn, int? chargeLimitSoc)
    {
        var c = CountedClosures(bcs);
        return string.Join("|",
            (int)c.FrontDriverDoor,
            (int)c.FrontPassengerDoor,
            (int)c.RearDriverDoor,
            (int)c.RearPassengerDoor,
            (int)c.FrontTrunk,
            (int)c.RearTrunk,
            (int)bcs.UserPresence,
            pluggedIn?.ToString() ?? "null",
            chargeLimitSoc?.ToString() ?? "null");
    }

    /// <summary>
    /// True if all counted closures (four doors, front trunk and rear trunk) are closed. The charge port door and
    /// tonneau are intentionally ignored.
    /// </summary>
    private static bool AllCountedClosuresClosed(VehicleStatus bcs)
    {
        var c = CountedClosures(bcs);
        return c.FrontDriverDoor == ClosureState.ClosurestateClosed
               && c.FrontPassengerDoor == ClosureState.ClosurestateClosed
               && c.RearDriverDoor == ClosureState.ClosurestateClosed
               && c.RearPassengerDoor == ClosureState.ClosurestateClosed
               && c.FrontTrunk == ClosureState.ClosurestateClosed
               && c.RearTrunk == ClosureState.ClosurestateClosed;
    }

    /// <summary>
    /// The closure states of a car, substituting the default instance when the car reported none.
    /// </summary>
    /// <remarks>
    /// A fully closed car sends no closure data at all: CLOSURESTATE_CLOSED is 0 in Tesla's VCSEC proto and the
    /// container marshals with protojson's default options, which omit every field holding its proto3 default. The
    /// default instance says exactly that - every closure closed - so absent and closed need no special casing, which
    /// is the whole reason this reads Tesla's generated types instead of hand written DTOs.
    /// </remarks>
    private static ClosureStatuses CountedClosures(VehicleStatus bcs) => bcs.ClosureStatuses ?? new ClosureStatuses();

    private static bool IsOccupantPresent(VehicleStatus bcs) =>
        bcs.UserPresence == UserPresence.VehicleUserPresencePresent;

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
