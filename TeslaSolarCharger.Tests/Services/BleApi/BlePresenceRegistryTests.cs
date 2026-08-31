using Microsoft.Extensions.Logging.Abstractions;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using System.Linq;
using TeslaSolarCharger.BleApi.Dtos.Worker;
using TeslaSolarCharger.BleApi.Services;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.BleApi;

/// <summary>
/// The registry is where presence is decided. It used to live in the Go worker where only a car in the driveway could
/// exercise it; here every rule is an ordinary test.
///
/// The rule under test throughout: lastSeen = max(lastAdvertisement, lastCommandSuccess). A Tesla emits nothing at
/// all while it holds a connection to us, so advertisements are reliable exactly while no link exists and command
/// outcomes are available exactly while one does.
/// </summary>
public class BlePresenceRegistryTests
{
    private const string Adapter = "2C:CF:67:23:71:79";
    private const string Car11Vin = "LRW3E7FA9MC239068";
    private const string Car10Vin = "LRW3E7EK5TC770390";
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(90);
    private static readonly DateTimeOffset Start = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private static BlePresenceRegistry CreateRegistry() => new(NullLogger<BlePresenceRegistry>.Instance);

    private static WorkerResponse Digest(int total, params WorkerDeviceObservation[] devices) => new()
    {
        Kind = "adv",
        WindowMs = 500,
        Total = total,
        Devices = devices.ToList(),
    };

    private static WorkerDeviceObservation Device(string address, string? name, int count, int named, int rssi = -65) => new()
    {
        Addr = address,
        Name = name,
        Count = count,
        Named = named,
        Rssi = rssi,
        Connectable = true,
    };

    /// <summary>
    /// Pinned against the real cars: the container computes the advertised name from the VIN, and if this drifts from
    /// the Go implementation every car silently stops being recognized.
    /// </summary>
    [Theory]
    [InlineData(Car11Vin, "S612fafca57f07c21C")]
    [InlineData(Car10Vin, "Se9d7ec89b27c1e95C")]
    public void ComputesTheLocalNameACarAdvertisesUnder(string vin, string expected)
    {
        Assert.Equal(expected, BlePresenceRegistry.VehicleLocalName(vin));
    }

    [Fact]
    public void RecognizesOnlyNamesOfTheVehicleShape()
    {
        Assert.True(BlePresenceRegistry.IsVehicleLocalName(BlePresenceRegistry.VehicleLocalName(Car11Vin)));
        foreach (var name in new[] { null, "", "some-phone", "S0011223344556677", "0011223344556677C", "SZZ11223344556677C" })
        {
            Assert.False(BlePresenceRegistry.IsVehicleLocalName(name));
        }
    }

    private static BlePresenceRegistry Observing(DateTimeOffset at)
    {
        var registry = CreateRegistry();
        registry.ApplyScanState(Adapter, "running", null, at);
        return registry;
    }

    private static DtoBlePresenceVehicle Presence(BlePresenceRegistry registry, string vin, DateTimeOffset now) =>
        registry.GetPresence(Adapter, new List<string> { vin }, MaxAge, now).Vehicles.Single();

    [Fact]
    public void ACarHeardRecentlyIsPresent()
    {
        var registry = Observing(Start);
        var name = BlePresenceRegistry.VehicleLocalName(Car11Vin);
        registry.ApplyDigest(Adapter, Digest(12, Device("90:2e:ab:23:19:4a", name, 12, 5)), Start.AddMinutes(3));

        var presence = Presence(registry, Car11Vin, Start.AddMinutes(3).AddSeconds(10));
        Assert.True(presence.Heard);
        Assert.Equal(10000, presence.LastSeenMsAgo);
        Assert.Equal(12, presence.Count);
        Assert.Equal(5, presence.NamedCount);
        //The nameless advertisements of the same window belong to the car as well.
        Assert.Equal(7, presence.AddressCount);
        Assert.Equal("90:2e:ab:23:19:4a", presence.Address);
    }

    [Fact]
    public void ACarNotHeardWithinTheMaxAgeIsNotPresent()
    {
        var registry = Observing(Start);
        var name = BlePresenceRegistry.VehicleLocalName(Car11Vin);
        registry.ApplyDigest(Adapter, Digest(1, Device("90:2e:ab:23:19:4a", name, 1, 1)), Start.AddMinutes(3));

        Assert.True(Presence(registry, Car11Vin, Start.AddMinutes(3).AddSeconds(89)).Heard);
        Assert.False(Presence(registry, Car11Vin, Start.AddMinutes(3).AddSeconds(91)).Heard);
    }

    [Fact]
    public void ACarThatWasNeverHeardIsNotPresent()
    {
        var registry = Observing(Start);
        registry.ApplyDigest(Adapter, Digest(3, Device("11:11:11:11:11:11", "some-phone", 3, 3)), Start.AddMinutes(3));

        var presence = Presence(registry, Car11Vin, Start.AddMinutes(3));
        Assert.False(presence.Heard);
        Assert.Null(presence.LastSeenMsAgo);
    }

    /// <summary>
    /// The measured reason the old design failed: most of a Tesla's advertisements carry no local name (55-61 %), so
    /// a name only matcher throws most of the car's traffic away.
    /// </summary>
    [Fact]
    public void NamelessAdvertisementsOfALearnedAddressCountForTheCar()
    {
        var registry = Observing(Start);
        var name = BlePresenceRegistry.VehicleLocalName(Car11Vin);
        var at = Start.AddMinutes(3);

        //Before anything identified the address, its nameless traffic belongs to nobody.
        registry.ApplyDigest(Adapter, Digest(4, Device("90:2e:ab:23:19:4a", null, 4, 0)), at);
        Assert.False(Presence(registry, Car11Vin, at).Heard);

        registry.ApplyDigest(Adapter, Digest(2, Device("90:2e:ab:23:19:4a", name, 2, 2)), at.AddSeconds(1));
        registry.ApplyDigest(Adapter, Digest(9, Device("90:2e:ab:23:19:4a", null, 9, 0)), at.AddSeconds(2));

        var presence = Presence(registry, Car11Vin, at.AddSeconds(2));
        Assert.True(presence.Heard);
        Assert.Equal(11, presence.Count);
        Assert.Equal(2, presence.NamedCount);
        Assert.Equal(9, presence.AddressCount);
        Assert.Equal("address", presence.LastSource);
    }

    [Fact]
    public void ALearnedAddressStopsCountingAfterTheBindingExpires()
    {
        var registry = Observing(Start);
        var name = BlePresenceRegistry.VehicleLocalName(Car11Vin);
        var at = Start.AddMinutes(3);
        registry.ApplyDigest(Adapter, Digest(1, Device("90:2e:ab:23:19:4a", name, 1, 1)), at);
        registry.ApplyDigest(Adapter, Digest(5, Device("90:2e:ab:23:19:4a", null, 5, 0)), at.Add(BlePresenceRegistry.AddressBindingTtl).AddSeconds(1));

        var presence = Presence(registry, Car11Vin, at.Add(BlePresenceRegistry.AddressBindingTtl).AddSeconds(1));
        Assert.Equal(1, presence.Count);
    }

    /// <summary>
    /// A rotated address must stop counting, otherwise a device that inherits it would be reported as a car at home.
    /// </summary>
    [Fact]
    public void RebindingDropsThePreviousAddress()
    {
        var registry = Observing(Start);
        var name = BlePresenceRegistry.VehicleLocalName(Car11Vin);
        var at = Start.AddMinutes(3);
        registry.ApplyDigest(Adapter, Digest(1, Device("90:2e:ab:23:19:4a", name, 1, 1)), at);
        registry.ApplyDigest(Adapter, Digest(1, Device("aa:bb:cc:dd:ee:ff", name, 1, 1)), at.AddSeconds(1));
        registry.ApplyDigest(Adapter, Digest(7, Device("90:2e:ab:23:19:4a", null, 7, 0)), at.AddSeconds(2));

        var presence = Presence(registry, Car11Vin, at.AddSeconds(2));
        Assert.Equal(2, presence.Count);
        Assert.Equal("aa:bb:cc:dd:ee:ff", presence.Address);
    }

    /// <summary>
    /// The load bearing half of the design: a polled car is silent because our own connection silences it, so its
    /// presence has to come from the commands it answers.
    /// </summary>
    [Fact]
    public void ACommandTheCarAnsweredCountsAsPresence()
    {
        var registry = Observing(Start);
        var at = Start.AddMinutes(3);
        registry.NoteCommandOutcome(Adapter, Car11Vin, BleCommandOutcome.Ok, at);

        var presence = Presence(registry, Car11Vin, at.AddSeconds(30));
        Assert.True(presence.Heard);
        Assert.Equal(30000, presence.LastSeenMsAgo);
        Assert.Equal("command", presence.LastSource);
        //Command traffic is not radio evidence and must not pretend to be.
        Assert.Equal(0, presence.Count);
        Assert.Null(presence.LastAdvertisementMsAgo);
    }

    [Theory]
    [InlineData(BleCommandOutcome.Ok)]
    [InlineData(BleCommandOutcome.CarRefused)]
    [InlineData(BleCommandOutcome.CarAsleep)]
    public void EveryOutcomeThatProvesTheCarAnsweredCountsAsPresence(BleCommandOutcome outcome)
    {
        var registry = Observing(Start);
        registry.NoteCommandOutcome(Adapter, Car11Vin, outcome, Start.AddMinutes(3));
        Assert.True(Presence(registry, Car11Vin, Start.AddMinutes(3)).Heard);
    }

    [Theory]
    [InlineData(BleCommandOutcome.LinkFailed)]
    [InlineData(BleCommandOutcome.CarAbsent)]
    [InlineData(BleCommandOutcome.WorkerError)]
    [InlineData(BleCommandOutcome.AdapterUnavailable)]
    public void AFailedCommandSaysNothingAboutPresence(BleCommandOutcome outcome)
    {
        var registry = Observing(Start);
        registry.NoteCommandOutcome(Adapter, Car11Vin, outcome, Start.AddMinutes(3));
        Assert.False(Presence(registry, Car11Vin, Start.AddMinutes(3)).Heard);
    }

    /// <summary>
    /// The two sources are complementary: while we hold a link the car is silent and the commands carry presence,
    /// and the advertisement age keeps growing meanwhile without ever making the car look absent.
    /// </summary>
    [Fact]
    public void CommandEvidenceCarriesPresenceWhileTheCarIsSilencedByOurConnection()
    {
        var registry = Observing(Start);
        var name = BlePresenceRegistry.VehicleLocalName(Car11Vin);
        var at = Start.AddMinutes(3);
        registry.ApplyDigest(Adapter, Digest(1, Device("90:2e:ab:23:19:4a", name, 1, 1)), at);

        //Two minutes of polling with no advertisement at all, which is what a connected car looks like.
        for (var second = 13; second <= 120; second += 13)
        {
            registry.NoteCommandOutcome(Adapter, Car11Vin, BleCommandOutcome.Ok, at.AddSeconds(second));
        }

        var presence = Presence(registry, Car11Vin, at.AddSeconds(125));
        Assert.True(presence.Heard);
        Assert.Equal(8000, presence.LastSeenMsAgo);
        //Advertisements alone would have aged past the threshold long ago.
        Assert.Equal(125000, presence.LastAdvertisementMsAgo);
    }

    /// <summary>
    /// Without this, every container or worker restart would report every car as never heard and mark it away.
    /// </summary>
    [Fact]
    public void NothingIsConcludedWhileTheScanIsStillWarmingUp()
    {
        var registry = Observing(Start);
        var result = registry.GetPresence(Adapter, new List<string> { Car11Vin }, MaxAge, Start.AddSeconds(30));
        Assert.True(result.WarmingUp);
        Assert.True(registry.WasHeardWithin(Adapter, Car11Vin, MaxAge, Start.AddSeconds(30)));

        var settled = registry.GetPresence(Adapter, new List<string> { Car11Vin }, MaxAge, Start.AddSeconds(91));
        Assert.False(settled.WarmingUp);
        Assert.False(registry.WasHeardWithin(Adapter, Car11Vin, MaxAge, Start.AddSeconds(91)));
    }

    [Fact]
    public void APauseForACommandDoesNotRestartTheWarmUp()
    {
        var registry = Observing(Start);
        registry.ApplyScanState(Adapter, "paused", "radio handed over", Start.AddSeconds(60));
        registry.ApplyScanState(Adapter, "running", null, Start.AddSeconds(61));

        //Handing the radio over for a command lasts milliseconds and the command itself is presence evidence, so it
        //must not reset the observation window and make every car unknown again.
        Assert.False(registry.GetPresence(Adapter, new List<string> { Car11Vin }, MaxAge, Start.AddSeconds(91)).WarmingUp);
    }

    [Fact]
    public void AStoppedScanMakesEveryAnswerUnknownAgain()
    {
        var registry = Observing(Start);
        registry.ApplyScanState(Adapter, "stopped", null, Start.AddSeconds(120));
        var result = registry.GetPresence(Adapter, new List<string> { Car11Vin }, MaxAge, Start.AddSeconds(130));

        Assert.False(result.ScannerRunning);
        Assert.True(result.WarmingUp);
        Assert.True(registry.WasHeardWithin(Adapter, Car11Vin, MaxAge, Start.AddSeconds(130)));
    }

    /// <summary>
    /// A worker restart is not a car leaving: the per car history has to survive it, only the observation window is
    /// dropped.
    /// </summary>
    [Fact]
    public void PresenceSurvivesAWorkerRestart()
    {
        var registry = Observing(Start);
        var name = BlePresenceRegistry.VehicleLocalName(Car11Vin);
        registry.ApplyDigest(Adapter, Digest(1, Device("90:2e:ab:23:19:4a", name, 1, 1)), Start.AddMinutes(3));
        registry.ForgetAdapter(Adapter);
        registry.ApplyScanState(Adapter, "running", null, Start.AddMinutes(3).AddSeconds(5));

        var presence = Presence(registry, Car11Vin, Start.AddMinutes(3).AddSeconds(10));
        Assert.Equal(10000, presence.LastSeenMsAgo);
    }

    [Fact]
    public void ReportsTheRadioEvidenceThatTellsAWorkingRadioFromADeafOne()
    {
        var registry = Observing(Start);
        registry.ApplyDigest(Adapter, Digest(30,
            Device("11:11:11:11:11:11", "some-phone", 20, 20),
            Device("22:22:22:22:22:22", null, 10, 0)), Start.AddSeconds(100));

        var result = registry.GetPresence(Adapter, new List<string>(), MaxAge, Start.AddSeconds(101));
        Assert.Equal(30, result.AdvertisementsSeen);
        Assert.Equal(2, result.DistinctDevicesSeen);
        Assert.Equal(1000, result.LastAdvertisementMsAgo);
        Assert.True(result.ScannerRunning);
    }

    /// <summary>
    /// The advertisement total outlives a worker restart while the observation window does not, so the rate has to be
    /// computed over the current window. Measured on the bench before this was fixed: 400 570 advertisements counted
    /// over five hours divided by a 42 second window reported 9554 advertisements per second.
    /// </summary>
    [Fact]
    public void TheAdvertisementRateIsMeasuredOverTheCurrentObservationWindow()
    {
        var registry = Observing(Start);
        registry.ApplyDigest(Adapter, Digest(6000, Device("11:11:11:11:11:11", "some-phone", 6000, 6000)), Start.AddMinutes(50));

        //A worker restart drops the observation window but keeps the totals.
        registry.ForgetAdapter(Adapter);
        registry.ApplyScanState(Adapter, "running", null, Start.AddHours(1));
        registry.ApplyDigest(Adapter, Digest(20, Device("11:11:11:11:11:11", "some-phone", 20, 20)), Start.AddHours(1).AddSeconds(5));

        var result = registry.GetPresence(Adapter, new List<string>(), MaxAge, Start.AddHours(1).AddSeconds(10));
        Assert.Equal(6020, result.AdvertisementsSeen);
        Assert.Equal(2, result.AdvertisementsPerSecond);
    }

    [Fact]
    public void AnAdapterThatHearsNothingAtAllIsReportedAsDeaf()
    {
        var registry = Observing(Start);
        var threshold = TimeSpan.FromSeconds(90);
        Assert.False(registry.IsDeaf(Adapter, threshold, Start.AddSeconds(80)));
        Assert.True(registry.IsDeaf(Adapter, threshold, Start.AddSeconds(91)));

        registry.ApplyDigest(Adapter, Digest(1, Device("11:11:11:11:11:11", "some-phone", 1, 1)), Start.AddSeconds(95));
        Assert.False(registry.IsDeaf(Adapter, threshold, Start.AddSeconds(100)));
    }

    [Fact]
    public void AStoppedScannerIsNotReportedAsDeaf()
    {
        var registry = CreateRegistry();
        //Nothing is listening, so hearing nothing says nothing about the adapter.
        Assert.False(registry.IsDeaf(Adapter, TimeSpan.FromSeconds(90), Start.AddHours(1)));
    }

    /// <summary>
    /// Measured on the live system: while a car is polled every 13 s the worker holds a link to it and this adapter's
    /// advertisement total stops moving entirely, for minutes, while every command is answered. Judging deafness by
    /// advertisements alone therefore restarted a working worker every cooldown for as long as a car was charging,
    /// and each restart blinded presence for a full max age.
    /// </summary>
    [Fact]
    public void AnAdapterThatAnswersCommandsIsNotDeafHoweverLongItHeardNoAdvertisement()
    {
        var registry = Observing(Start);
        var threshold = TimeSpan.FromSeconds(90);
        //Four minutes of polling with a held link, which is what a charging car looks like on the radio.
        for (var second = 13; second <= 240; second += 13)
        {
            registry.NoteCommandOutcome(Adapter, Car11Vin, BleCommandOutcome.Ok, Start.AddSeconds(second));
        }
        Assert.False(registry.IsDeaf(Adapter, threshold, Start.AddSeconds(245)));

        //Once the commands stop too, nothing is reaching the adapter at all and it really is deaf.
        Assert.True(registry.IsDeaf(Adapter, threshold, Start.AddSeconds(330)));
    }

    /// <summary>
    /// Only outcomes that prove the car answered are evidence the radio works: a link that failed says nothing about
    /// whether the adapter can hear.
    /// </summary>
    [Fact]
    public void AFailedCommandIsNoEvidenceThatTheAdapterHears()
    {
        var registry = Observing(Start);
        registry.NoteCommandOutcome(Adapter, Car11Vin, BleCommandOutcome.LinkFailed, Start.AddSeconds(85));
        Assert.True(registry.IsDeaf(Adapter, TimeSpan.FromSeconds(90), Start.AddSeconds(91)));
    }

    /// <summary>
    /// A restart gives the adapter a fresh observation window, and that window has to prove itself on its own
    /// evidence. Without this a restart is judged by what an earlier window heard.
    /// </summary>
    [Fact]
    public void AFreshObservationWindowIsNotJudgedByTheEvidenceOfAnEarlierOne()
    {
        var registry = Observing(Start);
        registry.NoteCommandOutcome(Adapter, Car11Vin, BleCommandOutcome.Ok, Start.AddSeconds(10));
        registry.ApplyDigest(Adapter, Digest(1, Device("11:11:11:11:11:11", "some-phone", 1, 1)), Start.AddSeconds(10));

        registry.ForgetAdapter(Adapter);
        registry.ApplyScanState(Adapter, "running", null, Start.AddMinutes(30));

        var threshold = TimeSpan.FromSeconds(90);
        Assert.False(registry.IsDeaf(Adapter, threshold, Start.AddMinutes(30).AddSeconds(80)));
        Assert.True(registry.IsDeaf(Adapter, threshold, Start.AddMinutes(30).AddSeconds(91)));
    }

    [Fact]
    public void EveryHeardCarIsTrackedWhetherOrNotItWasAskedAbout()
    {
        var registry = Observing(Start);
        registry.ApplyDigest(Adapter, Digest(2,
            Device("90:2e:ab:23:19:4a", BlePresenceRegistry.VehicleLocalName(Car11Vin), 1, 1),
            Device("44:3e:8a:63:4f:0f", BlePresenceRegistry.VehicleLocalName(Car10Vin), 1, 1)), Start.AddMinutes(3));

        var result = registry.GetPresence(Adapter, new List<string> { Car11Vin }, MaxAge, Start.AddMinutes(3));
        Assert.Single(result.Vehicles);
        Assert.Equal(2, result.Tracked.Count);
    }

    [Fact]
    public void TheRegistryStaysBounded()
    {
        var registry = Observing(Start);
        for (var index = 0; index < BlePresenceRegistry.MaxTrackedVehicles * 3; index++)
        {
            registry.ApplyDigest(Adapter, Digest(1,
                Device($"aa:aa:aa:aa:aa:{index:x2}", BlePresenceRegistry.VehicleLocalName($"VIN{index}"), 1, 1)),
                Start.AddMinutes(3));
        }
        var result = registry.GetPresence(Adapter, new List<string>(), MaxAge, Start.AddMinutes(3));
        Assert.True(result.Tracked.Count <= BlePresenceRegistry.MaxTrackedVehicles);
    }
}
