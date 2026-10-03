using PkSoftwareService.Custom.Backend.Ble;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using TeslaSolarCharger.BleApi.Dtos.Worker;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

/// <summary>
/// Everything the container knows about which cars are around, built from the worker's advertisement stream. All the
/// rules live here rather than in the Go worker: the worker is a radio device driver that reports what it heard, and
/// every decision made from that is an ordinary unit test instead of code that can only be exercised with a car in
/// the driveway.
///
/// The rule the whole design reduces to:
///
///     lastSeen(vin) = max(lastAdvertisement(vin), lastCommandSuccess(vin))
///
/// A Tesla emits nothing at all while it holds a connection to us (measured: 0 advertisements in 11 of 11 samples,
/// control car unaffected), so advertisements are reliable exactly while no link exists and command outcomes are
/// available exactly while one does. The two sources are complementary, so both are only missing when the car really
/// is gone.
///
/// State lives here and not in the worker, so presence survives a worker restart.
/// </summary>
public class BlePresenceRegistry : IBlePresenceRegistry
{
    /// <summary>
    /// A car whose local name only travels in the scan response is heard as a nameless advertisement most of the
    /// time (55-61 % measured on both cars). Once a named advertisement confirms which address belongs to the car,
    /// its bare advertisements count too - until the binding expires, so a rotated address cannot be inherited by
    /// another device for long. Only a named advertisement ever creates or renews a binding.
    /// </summary>
    public static readonly TimeSpan AddressBindingTtl = TimeSpan.FromMinutes(10);

    //Bounded so a site with a lot of Bluetooth traffic cannot grow the registry without limit.
    public const int MaxTrackedVehicles = 32;
    public const int MaxTrackedDevices = 4096;

    public const string SourceAdvertisement = "advertisement";
    public const string SourceAddress = "address";
    public const string SourceCommand = "command";

    private readonly ILogger<BlePresenceRegistry> _logger;
    private readonly ConcurrentDictionary<string, AdapterState> _adapters = new(StringComparer.OrdinalIgnoreCase);

    public BlePresenceRegistry(ILogger<BlePresenceRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The name a Tesla advertises under: "S" plus the first eight bytes of the VIN's SHA1 as lower case hex plus
    /// "C". Mirrors VehicleLocalName of the vehicle-command library; the hash cannot be inverted, so the registry
    /// tracks whatever it hears and only translates VIN to local name when a question is asked.
    /// </summary>
    public static string VehicleLocalName(string vin)
    {
        var digest = SHA1.HashData(Encoding.UTF8.GetBytes(vin));
        return string.Concat("S", Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant(), "C");
    }

    private sealed class VehicleRecord
    {
        public required string LocalName { get; init; }
        public DateTimeOffset? LastAdvertisementUtc;
        public DateTimeOffset? LastCommandSuccessUtc;
        public DateTimeOffset? FirstHeardUtc;
        public DateTimeOffset? AddressConfirmedUtc;
        public string? Address;
        public int? Rssi;
        public bool? Connectable;
        public long Count;
        public long NamedCount;
        public long AddressCount;
        public string? LastSource;

        public DateTimeOffset? LastSeenUtc =>
            LastAdvertisementUtc is { } advertisement && LastCommandSuccessUtc is { } command
                ? (advertisement > command ? advertisement : command)
                : LastAdvertisementUtc ?? LastCommandSuccessUtc;
    }

    private sealed class AdapterState
    {
        public readonly object Lock = new();
        public readonly Dictionary<string, VehicleRecord> ByLocalName = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> AddressToLocalName = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase);
        public long AdvertisementsSeen;
        /// <summary>
        /// The advertisement total when the current observation window began. The total itself is for the registry's
        /// whole life and outlives a worker restart, so a rate computed from it against the current window would
        /// divide hours of counting by seconds of observing.
        /// </summary>
        public long AdvertisementsAtObservingStart;
        public DateTimeOffset? LastAdvertisementUtc;
        /// <summary>
        /// When a car last answered a command on this adapter. Kept next to the advertisement timestamp because it is
        /// the other half of the evidence that the radio works: see <see cref="IsDeaf"/>.
        /// </summary>
        public DateTimeOffset? LastCommandSuccessUtc;
        public bool ScanRunning;
        /// <summary>
        /// When the scan last became available. Presence answers are "unknown" until it has been observing for a
        /// full max age: without that, every container or worker restart would report every car as never heard and
        /// mark them away.
        /// </summary>
        public DateTimeOffset? ObservingSinceUtc;
        public string? LastScanError;
        /// <summary>When the scan last reported running; how long it then lasted tells a working scan from a dead one.</summary>
        public DateTimeOffset? ScanStartedUtc;
        /// <summary>
        /// When the current run of failing scans began, null while the scan works. A run only consists of scans that
        /// failed within <see cref="HealthyScanDuration"/> of starting.
        /// </summary>
        public DateTimeOffset? ScanFailingSinceUtc;
        /// <summary>How many scans in a row failed with <see cref="RepeatedScanError"/>.</summary>
        public int RepeatedScanErrorCount;
        public string? RepeatedScanError;
    }

    /// <summary>
    /// A scan that ran at least this long before failing worked in between, so its error starts a new run of failures
    /// instead of extending one. The worker re-arms a failed scan after one second, and a stuck adapter fails within a
    /// millisecond of every re-arm.
    /// </summary>
    public static readonly TimeSpan HealthyScanDuration = TimeSpan.FromSeconds(5);

    private AdapterState GetState(string adapterKey) => _adapters.GetOrAdd(adapterKey, _ => new AdapterState());

    public void ApplyDigest(string adapterKey, WorkerResponse digest, DateTimeOffset at)
    {
        var state = GetState(adapterKey);
        lock (state.Lock)
        {
            state.AdvertisementsSeen += digest.Total;
            if (digest.Total > 0)
            {
                state.LastAdvertisementUtc = at;
                //Anything heard proves the scan works.
                ClearScanFailures(state);
            }
            foreach (var device in digest.Devices ?? new List<WorkerDeviceObservation>())
            {
                ApplyDevice(state, device, at);
            }
        }
    }

    private static void ApplyDevice(AdapterState state, WorkerDeviceObservation device, DateTimeOffset at)
    {
        if (string.IsNullOrEmpty(device.Addr))
        {
            return;
        }
        if (state.Devices.Count < MaxTrackedDevices)
        {
            state.Devices.Add(device.Addr);
        }
        var named = !string.IsNullOrEmpty(device.Name) && IsVehicleLocalName(device.Name);
        string localName;
        if (named)
        {
            localName = device.Name!;
        }
        else if (string.IsNullOrEmpty(device.Name)
                 && state.AddressToLocalName.TryGetValue(device.Addr, out var bound)
                 && state.ByLocalName.TryGetValue(bound, out var boundRecord)
                 && boundRecord.AddressConfirmedUtc is { } confirmed
                 && at - confirmed <= AddressBindingTtl)
        {
            localName = bound;
        }
        else
        {
            //Someone else's device, or a car we have not identified yet. It still counts as radio evidence through
            //the adapter wide totals.
            return;
        }

        if (!state.ByLocalName.TryGetValue(localName, out var record))
        {
            if (state.ByLocalName.Count >= MaxTrackedVehicles)
            {
                return;
            }
            record = new VehicleRecord { LocalName = localName, FirstHeardUtc = at };
            state.ByLocalName[localName] = record;
        }
        record.Count += device.Count;
        record.Rssi = device.Rssi;
        record.Connectable = device.Connectable;
        record.LastAdvertisementUtc = at;
        record.FirstHeardUtc ??= at;
        if (named)
        {
            //The window carried the name at least once; the rest of that address's advertisements in the same window
            //were nameless and are attributed to the car for the same reason a learned address is.
            record.NamedCount += device.Named;
            record.AddressCount += Math.Max(0, device.Count - device.Named);
            record.LastSource = SourceAdvertisement;
            if (!string.Equals(record.Address, device.Addr, StringComparison.OrdinalIgnoreCase))
            {
                //Rebinding drops the previous address so a rotated one cannot keep counting for this car.
                if (record.Address != null)
                {
                    state.AddressToLocalName.Remove(record.Address);
                }
                record.Address = device.Addr;
            }
            record.AddressConfirmedUtc = at;
            state.AddressToLocalName[device.Addr] = localName;
        }
        else
        {
            record.AddressCount += device.Count;
            record.LastSource = SourceAddress;
        }
    }

    public void ApplyScanState(string adapterKey, string? scanState, string? reason, DateTimeOffset at)
    {
        var state = GetState(adapterKey);
        lock (state.Lock)
        {
            switch (scanState)
            {
                case "running":
                    state.ScanRunning = true;
                    state.LastScanError = null;
                    state.ScanStartedUtc = at;
                    //A pause for a command is not a break in observation - it lasts milliseconds and the command
                    //itself is presence evidence - so the observation window only starts when there is none yet.
                    if (state.ObservingSinceUtc is null)
                    {
                        state.ObservingSinceUtc = at;
                        state.AdvertisementsAtObservingStart = state.AdvertisementsSeen;
                    }
                    break;
                case "paused":
                    //Deliberately keeps ObservingSinceUtc: see above.
                    //Only a scan that was running can be handed over, so the scan worked.
                    ClearScanFailures(state);
                    break;
                default:
                    state.ScanRunning = false;
                    state.ObservingSinceUtc = null;
                    if (scanState == "error")
                    {
                        state.LastScanError = reason;
                        RegisterScanFailure(state, reason, at);
                    }
                    break;
            }
        }
    }

    private static void RegisterScanFailure(AdapterState state, string? reason, DateTimeOffset at)
    {
        var ranHealthy = state.ScanStartedUtc is { } started && at - started >= HealthyScanDuration;
        if (ranHealthy)
        {
            ClearScanFailures(state);
        }
        state.ScanFailingSinceUtc ??= at;
        if (state.RepeatedScanErrorCount > 0 && string.Equals(state.RepeatedScanError, reason, StringComparison.Ordinal))
        {
            state.RepeatedScanErrorCount++;
        }
        else
        {
            state.RepeatedScanError = reason;
            state.RepeatedScanErrorCount = 1;
        }
    }

    private static void ClearScanFailures(AdapterState state)
    {
        state.ScanFailingSinceUtc = null;
        state.RepeatedScanErrorCount = 0;
        state.RepeatedScanError = null;
    }

    public string? GetStuckScanReason(string adapterKey, TimeSpan failingThreshold, int repeatedErrorLimit, DateTimeOffset now)
    {
        var state = GetState(adapterKey);
        lock (state.Lock)
        {
            if (state.ScanFailingSinceUtc is not { } failingSince)
            {
                return null;
            }
            if (state.ScanRunning && state.ScanStartedUtc is { } started && now - started >= HealthyScanDuration)
            {
                //Re-armed and running fine since: the failures are over, the next pause or digest clears them.
                return null;
            }
            if (repeatedErrorLimit > 0 && state.RepeatedScanErrorCount >= repeatedErrorLimit)
            {
                //A stuck HCI layer answers every re-arm with the error it stored, never a new one: go-ble keeps the
                //error of a failed event and returns it for every later command until the adapter is bound again.
                return $"the scan failed {state.RepeatedScanErrorCount} times in a row with the same error: {state.RepeatedScanError}";
            }
            if (now - failingSince > failingThreshold)
            {
                return $"the scan has been failing for {(int)(now - failingSince).TotalSeconds} s, last error: {state.LastScanError ?? state.RepeatedScanError}";
            }
            return null;
        }
    }

    public void NoteCommandOutcome(string adapterKey, string vin, BleCommandOutcome? outcome, DateTimeOffset at)
    {
        //Ok, a refusal, "asleep" and a rejected key all mean the car answered us, which is stronger evidence than any
        //advertisement. Everything else says nothing about presence.
        if (outcome is not (BleCommandOutcome.Ok or BleCommandOutcome.CarRefused or BleCommandOutcome.CarAsleep
            or BleCommandOutcome.KeyNotPaired))
        {
            return;
        }
        var state = GetState(adapterKey);
        var localName = VehicleLocalName(vin);
        lock (state.Lock)
        {
            if (!state.ByLocalName.TryGetValue(localName, out var record))
            {
                if (state.ByLocalName.Count >= MaxTrackedVehicles)
                {
                    return;
                }
                record = new VehicleRecord { LocalName = localName, FirstHeardUtc = at };
                state.ByLocalName[localName] = record;
            }
            //Deliberately does not touch the advertisement counters: those are radio evidence and have to stay that
            //way, otherwise a polled car would look like it was advertising when it was in fact silenced by our own
            //connection.
            record.LastCommandSuccessUtc = at;
            record.LastSource = SourceCommand;
            //The adapter wide stamp is not about this car: it records that the radio did something, which is what
            //tells a broken adapter apart from a quiet one.
            state.LastCommandSuccessUtc = at;
            //A car answered through this adapter, so its HCI layer is alive whatever the scan reported.
            ClearScanFailures(state);
        }
    }

    public bool WasHeardWithin(string adapterKey, string vin, TimeSpan maxAge, DateTimeOffset now)
    {
        var state = GetState(adapterKey);
        lock (state.Lock)
        {
            if (state.ObservingSinceUtc is not { } observingSince || now - observingSince < maxAge)
            {
                //Not observing long enough to conclude anything. Never report "not heard" from ignorance.
                return true;
            }
            return state.ByLocalName.TryGetValue(VehicleLocalName(vin), out var record)
                   && record.LastSeenUtc is { } lastSeen
                   && now - lastSeen <= maxAge;
        }
    }

    /// <summary>
    /// Whether the adapter has stopped receiving anything at all, which only a fresh adapter bind recovers from.
    ///
    /// Advertisement silence alone does not prove it. Measured on the live system: while a car is polled every 13 s
    /// the worker holds a link to it, and a held link silences this adapter's scan completely - the adapter wide
    /// advertisement total did not move for minutes while every command was answered. Judging deafness by
    /// advertisements alone therefore condemned a working adapter for doing exactly what a charging car makes it do,
    /// and the restart that followed blinded presence for a full max age every cooldown.
    ///
    /// A command the car answered is proof the radio transmits and receives, so it counts as hearing.
    /// </summary>
    public bool IsDeaf(string adapterKey, TimeSpan silenceThreshold, DateTimeOffset now)
    {
        var state = GetState(adapterKey);
        lock (state.Lock)
        {
            if (!state.ScanRunning || state.ObservingSinceUtc is not { } observingSince)
            {
                return false;
            }
            var lastEvidence = Latest(state.LastAdvertisementUtc, state.LastCommandSuccessUtc);
            //A fresh observation window gets the full threshold to prove itself rather than being judged by evidence
            //an earlier one collected.
            var lastHeard = lastEvidence is { } evidence && evidence > observingSince ? evidence : observingSince;
            return now - lastHeard > silenceThreshold;
        }
    }

    private static DateTimeOffset? Latest(DateTimeOffset? first, DateTimeOffset? second) =>
        first is { } left && second is { } right ? (left > right ? left : right) : first ?? second;

    public void ForgetAdapter(string adapterKey)
    {
        //Only the observation window is dropped: the worker is gone, so nothing is being heard any more and no car
        //may be judged absent until a new scan has been observing for a full max age. The per car history is kept,
        //which is what makes presence survive a worker restart.
        var state = GetState(adapterKey);
        lock (state.Lock)
        {
            state.ScanRunning = false;
            state.ObservingSinceUtc = null;
            //The next worker binds the adapter afresh, so failures of the old one say nothing about it.
            state.ScanStartedUtc = null;
            ClearScanFailures(state);
        }
    }

    public DtoBlePresenceResult GetPresence(string adapterKey, IReadOnlyList<string> vins, TimeSpan maxAge, DateTimeOffset now)
    {
        var state = GetState(adapterKey);
        lock (state.Lock)
        {
            var observingMs = state.ObservingSinceUtc is { } since ? (long)(now - since).TotalMilliseconds : 0;
            var result = new DtoBlePresenceResult
            {
                Adapter = adapterKey,
                ScannerRunning = state.ScanRunning,
                WarmingUp = state.ObservingSinceUtc is null || now - state.ObservingSinceUtc.Value < maxAge,
                ObservingMs = observingMs,
                MaxAgeMs = (long)maxAge.TotalMilliseconds,
                AdvertisementsSeen = state.AdvertisementsSeen,
                //Rate over the current observation window only, so it stays meaningful after a worker restart.
                AdvertisementsPerSecond = observingMs > 0
                    ? Math.Round((state.AdvertisementsSeen - state.AdvertisementsAtObservingStart) * 1000d / observingMs, 2)
                    : 0,
                DistinctDevicesSeen = state.Devices.Count,
                LastAdvertisementMsAgo = state.LastAdvertisementUtc is { } last
                    ? (long)(now - last).TotalMilliseconds
                    : null,
                LastScanError = state.LastScanError,
            };
            foreach (var vin in vins)
            {
                var localName = VehicleLocalName(vin);
                var vehicle = state.ByLocalName.TryGetValue(localName, out var record)
                    ? ToDto(record, maxAge, now)
                    : new DtoBlePresenceVehicle { LocalName = localName };
                vehicle.Vin = vin;
                result.Vehicles.Add(vehicle);
            }
            foreach (var record in state.ByLocalName.Values.OrderBy(r => r.LocalName, StringComparer.Ordinal))
            {
                result.Tracked.Add(ToDto(record, maxAge, now));
            }
            return result;
        }
    }

    private static DtoBlePresenceVehicle ToDto(VehicleRecord record, TimeSpan maxAge, DateTimeOffset now) => new()
    {
        LocalName = record.LocalName,
        Heard = record.LastSeenUtc is { } lastSeen && now - lastSeen <= maxAge,
        LastSeenMsAgo = Age(record.LastSeenUtc, now),
        LastAdvertisementMsAgo = Age(record.LastAdvertisementUtc, now),
        LastCommandSuccessMsAgo = Age(record.LastCommandSuccessUtc, now),
        FirstHeardMsAgo = Age(record.FirstHeardUtc, now),
        Rssi = record.Rssi,
        Address = record.Address,
        Connectable = record.Connectable,
        Count = record.Count,
        NamedCount = record.NamedCount,
        AddressCount = record.AddressCount,
        LastSource = record.LastSource,
    };

    private static long? Age(DateTimeOffset? value, DateTimeOffset now) =>
        value is { } moment ? (long)(now - moment).TotalMilliseconds : null;

    /// <summary>
    /// Whether a local name has the shape <see cref="VehicleLocalName"/> produces. Matching the shape instead of a
    /// registered VIN list keeps the registry VIN agnostic: it records every car it hears, so a car added while the
    /// container runs needs no handshake.
    /// </summary>
    public static bool IsVehicleLocalName(string? localName)
    {
        if (localName is not { Length: 18 } || localName[0] != 'S' || localName[^1] != 'C')
        {
            return false;
        }
        for (var index = 1; index < localName.Length - 1; index++)
        {
            var character = localName[index];
            var isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
            {
                return false;
            }
        }
        return true;
    }
}
