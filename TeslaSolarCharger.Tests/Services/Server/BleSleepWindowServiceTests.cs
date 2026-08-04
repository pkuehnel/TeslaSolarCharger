using System;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BleSleepWindowServiceTests
{
    private const int CarId = 1;
    private const int Window = 13;
    private const int Stability = 5;
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IBleSleepWindowService NewService()
        => new BleSleepWindowService(Mock.Of<ILogger<BleSleepWindowService>>());

    /// <summary>
    /// A closed up car as the BLE container really reports it: CLOSURESTATE_CLOSED is 0 in Tesla's VCSEC proto and
    /// protojson omits proto3 defaults, so a closed closure arrives as null and never as the literal string. Only the
    /// open charge port door survives serialization here, which also proves it does not block a sleep window.
    /// </summary>
    private static DtoBleBodyControllerState AllClosed(
        string userPresence = "VEHICLE_USER_PRESENCE_NOT_PRESENT",
        string? frontDriverDoor = null,
        string? chargePort = "CLOSURESTATE_OPEN")
        => new()
        {
            VehicleSleepStatus = "VEHICLE_SLEEP_STATUS_AWAKE",
            UserPresence = userPresence,
            ClosureStatuses = new DtoBleClosureStatuses
            {
                FrontDriverDoor = frontDriverDoor,
                ChargePort = chargePort,
            },
        };

    /// <summary>
    /// Simulates one refresh cycle: poll only when the service allows it (mirrors the real BleVehicleDataService flow).
    /// Returns true if the infotainment poll happened, false if it was withheld (car is inside a silent window).
    /// </summary>
    private static bool Cycle(IBleSleepWindowService svc, DtoBleBodyControllerState bcs, bool? pluggedIn, int? socLimit, DateTime now)
    {
        var poll = svc.ShouldPollInfotainment(CarId, now, Window);
        if (poll)
        {
            svc.ObserveFullPoll(CarId, bcs, pluggedIn, socLimit, now, Window, Stability);
        }
        return poll;
    }

    [Fact]
    public void EntersWindowAfterStability_AndChargePortOpenDoesNotBlock()
    {
        var svc = NewService();
        //Baseline poll, then a poll once the stability period elapsed: the window starts on that poll.
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0));
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability)));
        //Next cycle must be silent (inside the window) even though the charge port door is open.
        Assert.False(Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability).AddSeconds(30)));
    }

    [Fact]
    public void DoesNotEnterWindowBeforeStability()
    {
        var svc = NewService();
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0));
        //One second before the stability period elapses it must still poll (no window yet).
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability).AddSeconds(-1)));
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability).AddSeconds(30)));
    }

    [Fact]
    public void ChangeOfPluggedInResetsStability()
    {
        var svc = NewService();
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0));
        //At +5 min plugged in changes -> stability restarts here.
        Assert.True(Cycle(svc, AllClosed(), true, 80, T0.AddMinutes(5)));
        //At +9 min (only 4 min after the change) still no window.
        Assert.True(Cycle(svc, AllClosed(), true, 80, T0.AddMinutes(9)));
        Assert.True(Cycle(svc, AllClosed(), true, 80, T0.AddMinutes(9).AddSeconds(30)));
        //At +10 min (5 min after the change) the window starts, so the following cycle is silent.
        Assert.True(Cycle(svc, AllClosed(), true, 80, T0.AddMinutes(10)));
        Assert.False(Cycle(svc, AllClosed(), true, 80, T0.AddMinutes(10).AddSeconds(30)));
    }

    [Fact]
    public void ChangeOfChargeLimitResetsStability()
    {
        var svc = NewService();
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0));
        Assert.True(Cycle(svc, AllClosed(), false, 70, T0.AddMinutes(5)));
        //Only 4 min after the charge limit change -> no window yet.
        Assert.True(Cycle(svc, AllClosed(), false, 70, T0.AddMinutes(9)));
        Assert.True(Cycle(svc, AllClosed(), false, 70, T0.AddMinutes(10)));
        Assert.False(Cycle(svc, AllClosed(), false, 70, T0.AddMinutes(10).AddSeconds(30)));
    }

    [Fact]
    public void OpenDoorNeverStartsWindow()
    {
        var svc = NewService();
        var openDoor = AllClosed(frontDriverDoor: "CLOSURESTATE_OPEN");
        //Even long after the stability period the window never starts while a door is open.
        Assert.True(Cycle(svc, openDoor, false, 80, T0));
        Assert.True(Cycle(svc, openDoor, false, 80, T0.AddMinutes(Stability)));
        Assert.True(Cycle(svc, openDoor, false, 80, T0.AddMinutes(30)));
    }

    [Fact]
    public void OccupantNeverStartsWindow()
    {
        var svc = NewService();
        var occupied = AllClosed(userPresence: "VEHICLE_USER_PRESENCE_PRESENT");
        Assert.True(Cycle(svc, occupied, false, 80, T0));
        Assert.True(Cycle(svc, occupied, false, 80, T0.AddMinutes(Stability)));
        Assert.True(Cycle(svc, occupied, false, 80, T0.AddMinutes(30)));
    }

    /// <summary>
    /// The regression that made the feature inert on every car: an unplugged car with everything closed serializes to
    /// no closureStatuses at all, which used to be read as "not closed" and blocked the window forever.
    /// </summary>
    [Theory]
    [InlineData(false)] //property missing entirely (car does not set the submessage)
    [InlineData(true)]  //empty object (car sets the submessage, all closures at their default)
    public void MissingClosureStatusesMeansClosedAndStartsWindow(bool emptyObjectInsteadOfNull)
    {
        var svc = NewService();
        var noClosures = new DtoBleBodyControllerState
        {
            VehicleSleepStatus = "VEHICLE_SLEEP_STATUS_AWAKE",
            UserPresence = "VEHICLE_USER_PRESENCE_NOT_PRESENT",
            ClosureStatuses = emptyObjectInsteadOfNull ? new DtoBleClosureStatuses() : null,
        };
        Assert.True(Cycle(svc, noClosures, false, 80, T0));
        var status = svc.GetStatus(CarId, T0, Window, Stability);
        Assert.NotNull(status);
        Assert.True(status!.CarClosedAndEmpty);
        //Once the stability period elapsed the window starts, so the following cycle is silent.
        Assert.True(Cycle(svc, noClosures, false, 80, T0.AddMinutes(Stability)));
        Assert.False(Cycle(svc, noClosures, false, 80, T0.AddMinutes(Stability).AddSeconds(30)));
    }

    /// <summary>
    /// Absent and explicitly closed describe the same physical state, so switching between them must not be mistaken
    /// for activity and restart the stability period.
    /// </summary>
    [Fact]
    public void ExplicitClosedAndAbsentClosureAreTheSameState()
    {
        var svc = NewService();
        Assert.True(Cycle(svc, AllClosed(frontDriverDoor: null), false, 80, T0));
        Assert.True(Cycle(svc, AllClosed(frontDriverDoor: "CLOSURESTATE_CLOSED"), false, 80, T0.AddMinutes(Stability)));
        //Stability was not restarted by the differently spelled but identical state, so the window is running.
        Assert.False(Cycle(svc, AllClosed(frontDriverDoor: null), false, 80, T0.AddMinutes(Stability).AddSeconds(30)));
    }

    [Fact]
    public void WindowExpiryPollsOnceThenReEntersImmediately()
    {
        var svc = NewService();
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0));
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability))); //enters window
        var windowStart = T0.AddMinutes(Stability);
        Assert.False(Cycle(svc, AllClosed(), false, 80, windowStart.AddSeconds(30))); //silent
        //After the full window a single poll happens again...
        Assert.True(Cycle(svc, AllClosed(), false, 80, windowStart.AddMinutes(Window)));
        //...and because nothing changed a new window starts right away (no fresh stability wait).
        Assert.False(Cycle(svc, AllClosed(), false, 80, windowStart.AddMinutes(Window).AddSeconds(30)));
    }

    [Fact]
    public void ResetSleepWindowClearsSilenceAndStatus()
    {
        var svc = NewService();
        Cycle(svc, AllClosed(), false, 80, T0);
        Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability)); //in window
        svc.ResetSleepWindow(CarId);
        Assert.Null(svc.GetStatus(CarId, T0.AddMinutes(Stability), Window, Stability));
        //After a reset the next cycle polls again (fresh stability).
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability).AddSeconds(30)));
    }

    [Fact]
    public void ChargeCommandStyleResetPreventsSilenceAfterWindowStarted()
    {
        //Reproduces the race the explicit charging seam guards against: a window starts in the same cycle a charge
        //command is sent. The command resets the window, so the follow up read is not silenced.
        var svc = NewService();
        Cycle(svc, AllClosed(), false, 80, T0);
        Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability)); //window started
        svc.ResetSleepWindow(CarId); //simulates SendCommandToTeslaApi on a charge command
        Assert.True(Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability).AddSeconds(1)));
    }

    [Fact]
    public void DisabledFeatureNeverSilencesAndReportsNoStatus()
    {
        var svc = NewService();
        //Window minutes 0 disables the feature.
        Assert.True(svc.ShouldPollInfotainment(CarId, T0, 0));
        svc.ObserveFullPoll(CarId, AllClosed(), false, 80, T0, 0, Stability);
        Assert.True(svc.ShouldPollInfotainment(CarId, T0.AddMinutes(30), 0));
        Assert.Null(svc.GetStatus(CarId, T0, 0, Stability));
    }

    [Fact]
    public void AsleepThenWakeRestartsStability()
    {
        var svc = NewService();
        Cycle(svc, AllClosed(), false, 80, T0); //tracked, awake
        svc.NotifyAsleep(CarId);
        var asleep = svc.GetStatus(CarId, T0.AddMinutes(1), Window, Stability);
        Assert.NotNull(asleep);
        Assert.Equal(BleSleepPhase.Asleep, asleep!.Phase);

        //Car wakes again much later: the stability period must restart from the wake, not from the original arrival.
        var wake = T0.AddHours(8);
        Assert.True(Cycle(svc, AllClosed(), false, 80, wake));
        var afterWake = svc.GetStatus(CarId, wake, Window, Stability);
        Assert.NotNull(afterWake);
        Assert.Equal(BleSleepPhase.WaitingToSleep, afterWake!.Phase);
        //A poll one second before the stability elapses (measured from the wake) must still not be silent.
        Assert.True(Cycle(svc, AllClosed(), false, 80, wake.AddMinutes(Stability).AddSeconds(-1)));
        Assert.True(Cycle(svc, AllClosed(), false, 80, wake.AddMinutes(Stability)));
        Assert.False(Cycle(svc, AllClosed(), false, 80, wake.AddMinutes(Stability).AddSeconds(30)));
    }

    [Fact]
    public void StatusReportsBlockingClosureAndForbidsManualStart()
    {
        var svc = NewService();
        Cycle(svc, AllClosed(frontDriverDoor: "CLOSURESTATE_OPEN"), false, 80, T0);
        var status = svc.GetStatus(CarId, T0, Window, Stability);
        Assert.NotNull(status);
        Assert.Equal(BleSleepPhase.WaitingToSleep, status!.Phase);
        Assert.False(status.CarClosedAndEmpty);
        Assert.False(svc.TryStartWindowNow(CarId, T0, Window));
        //Refused start must not silence the car.
        Assert.True(Cycle(svc, AllClosed(frontDriverDoor: "CLOSURESTATE_OPEN"), false, 80, T0.AddSeconds(30)));
    }

    [Fact]
    public void StatusReportsBlockingOccupantAndForbidsManualStart()
    {
        var svc = NewService();
        Cycle(svc, AllClosed(userPresence: "VEHICLE_USER_PRESENCE_PRESENT"), false, 80, T0);
        var status = svc.GetStatus(CarId, T0, Window, Stability);
        Assert.NotNull(status);
        Assert.False(status!.CarClosedAndEmpty);
        Assert.False(svc.TryStartWindowNow(CarId, T0, Window));
    }

    [Fact]
    public void StatusReportsUnknownCarStateBeforeFirstFullPoll()
    {
        var svc = NewService();
        //NotifyAsleep tracks the car without ever having observed closures; after the wake nothing is known yet.
        svc.NotifyAsleep(CarId);
        var status = svc.GetStatus(CarId, T0, Window, Stability);
        Assert.NotNull(status);
        Assert.Equal(BleSleepPhase.Asleep, status!.Phase);
        Assert.Null(status.CarClosedAndEmpty);
        Assert.False(svc.TryStartWindowNow(CarId, T0, Window));
    }

    [Fact]
    public void ManualStartSkipsStabilityAndSilencesImmediately()
    {
        var svc = NewService();
        Cycle(svc, AllClosed(), false, 80, T0);
        var status = svc.GetStatus(CarId, T0, Window, Stability);
        Assert.NotNull(status);
        Assert.True(status!.CarClosedAndEmpty);

        //Long before the stability period elapsed the user starts the attempt manually.
        var manualStart = T0.AddMinutes(1);
        Assert.True(svc.TryStartWindowNow(CarId, manualStart, Window));
        var afterStart = svc.GetStatus(CarId, manualStart, Window, Stability);
        Assert.NotNull(afterStart);
        Assert.Equal(BleSleepPhase.TryingToSleep, afterStart!.Phase);
        Assert.Equal(Window * 60, afterStart.SecondsRemaining);
        //The next cycle is silent, and the window runs its full length from the manual start.
        Assert.False(Cycle(svc, AllClosed(), false, 80, manualStart.AddSeconds(30)));
        Assert.True(Cycle(svc, AllClosed(), false, 80, manualStart.AddMinutes(Window)));
    }

    [Fact]
    public void ManualStartIsRefusedForUntrackedAsleepAndAlreadyRunningWindow()
    {
        var svc = NewService();
        //Never polled: it is unknown whether the car is closed up.
        Assert.False(svc.TryStartWindowNow(CarId, T0, Window));

        Cycle(svc, AllClosed(), false, 80, T0);
        //Feature disabled.
        Assert.False(svc.TryStartWindowNow(CarId, T0, 0));

        svc.NotifyAsleep(CarId);
        Assert.False(svc.TryStartWindowNow(CarId, T0.AddMinutes(1), Window));

        //Awake again and inside a window: starting a second one makes no sense.
        var wake = T0.AddHours(1);
        Cycle(svc, AllClosed(), false, 80, wake);
        Assert.True(svc.TryStartWindowNow(CarId, wake, Window));
        Assert.False(svc.TryStartWindowNow(CarId, wake.AddSeconds(1), Window));
    }

    /// <summary>
    /// Guards the whole chain the way production runs it: container JSON -> BleVehicleDataService.DeserializeBodyControllerState
    /// -> sleep window state machine. The hand written DTOs the other tests use cannot catch a wrong assumption about
    /// the wire format, which is exactly how the "car is open or occupied" regression slipped through.
    /// </summary>
    [Fact]
    public void RealWorldClosedCarPayloadStartsWindow()
    {
        //Captured 2026-08-04 from a BLE container (Model 3, awake, locked, unplugged, all closures closed, nobody in
        //the car). Everything closed is the proto3 default, so the whole closureStatuses property is absent.
        const string json =
            "{\"vehicleLockState\":\"VEHICLELOCKSTATE_LOCKED\",\"vehicleSleepStatus\":\"VEHICLE_SLEEP_STATUS_AWAKE\",\"userPresence\":\"VEHICLE_USER_PRESENCE_NOT_PRESENT\"}";
        var bcs = BleVehicleDataService.DeserializeBodyControllerState(json);
        Assert.NotNull(bcs);
        Assert.Null(bcs!.ClosureStatuses);

        var svc = NewService();
        Assert.True(Cycle(svc, bcs, false, 80, T0));
        var status = svc.GetStatus(CarId, T0, Window, Stability);
        Assert.NotNull(status);
        Assert.True(status!.CarClosedAndEmpty, "A closed up car must not be reported as open or occupied");
        Assert.True(svc.TryStartWindowNow(CarId, T0, Window));
    }

    /// <summary>
    /// The counterpart: only a closure that is NOT closed survives protojson, so an open door shows up as the single
    /// populated property. Shape derived from Tesla's VCSEC proto (CLOSURESTATE_CLOSED = 0), not from a capture.
    /// </summary>
    [Fact]
    public void RealWorldOpenDoorPayloadBlocksWindow()
    {
        const string json =
            "{\"closureStatuses\":{\"frontDriverDoor\":\"CLOSURESTATE_OPEN\"},\"vehicleLockState\":\"VEHICLELOCKSTATE_UNLOCKED\",\"vehicleSleepStatus\":\"VEHICLE_SLEEP_STATUS_AWAKE\",\"userPresence\":\"VEHICLE_USER_PRESENCE_NOT_PRESENT\"}";
        var bcs = BleVehicleDataService.DeserializeBodyControllerState(json);
        Assert.NotNull(bcs);
        Assert.Equal("CLOSURESTATE_OPEN", bcs!.ClosureStatuses?.FrontDriverDoor);

        var svc = NewService();
        Assert.True(Cycle(svc, bcs, false, 80, T0));
        var status = svc.GetStatus(CarId, T0, Window, Stability);
        Assert.NotNull(status);
        Assert.False(status!.CarClosedAndEmpty);
        Assert.False(svc.TryStartWindowNow(CarId, T0, Window));
        //Still polling long after the stability period: an open door blocks the window indefinitely.
        Assert.True(Cycle(svc, bcs, false, 80, T0.AddMinutes(30)));
    }

    [Fact]
    public void StatusReportsWindowCountdown()
    {
        var svc = NewService();
        Cycle(svc, AllClosed(), false, 80, T0);
        Cycle(svc, AllClosed(), false, 80, T0.AddMinutes(Stability)); //window started at T0+5min
        var status = svc.GetStatus(CarId, T0.AddMinutes(Stability).AddMinutes(3), Window, Stability);
        Assert.NotNull(status);
        Assert.Equal(BleSleepPhase.TryingToSleep, status!.Phase);
        //13 min window, 3 min elapsed -> 10 min remaining.
        Assert.Equal(10 * 60, status.SecondsRemaining);
    }
}
