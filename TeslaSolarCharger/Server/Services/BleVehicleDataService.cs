using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using ChargeState = CarServer.ChargeState;
using ChargingStateCase = CarServer.ChargeState.Types.ChargingState.TypeOneofCase;
using VehicleData = CarServer.VehicleData;
using VehicleSleepStatus = VCSEC.VehicleSleepStatus_E;
using VehicleStatus = VCSEC.VehicleStatus;

namespace TeslaSolarCharger.Server.Services;

public class BleVehicleDataService(
    ILogger<BleVehicleDataService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ISettings settings,
    IBleService bleService,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    ICarPropertyUpdateHelper carPropertyUpdateHelper,
    ILoadPointManagementService loadPointManagementService,
    IErrorHandlingService errorHandlingService,
    IBlePresenceStateService blePresenceStateService,
    IBleReadCoordinator bleReadCoordinator,
    IBleSleepWindowService bleSleepWindowService,
    IBleAccessGateService bleAccessGateService,
    IIssueKeys issueKeys,
    IServiceScopeFactory serviceScopeFactory) : IBleVehicleDataService
{
    private static readonly TimeSpan RadioSilenceWarningDuration = TimeSpan.FromHours(24);

    public async Task RefreshBleCarData()
    {
        logger.LogTrace("{method}()", nameof(RefreshBleCarData));
        if (!configurationWrapper.GetVehicleDataViaBle() || !configurationWrapper.GetVehicleDataFromTesla())
        {
            return;
        }
        //Only cars that already switched to BLE data collection (Fleet Telemetry disabled on manual car config save)
        //are refreshed via BLE.
        var bleDataCarIds = await teslaSolarChargerContext.Cars
            .Where(c => (c.ShouldBeManaged == true)
                        && (c.CarType == CarType.Tesla)
                        && c.UseBle
                        && !c.UseFleetTelemetry
                        && !c.IncludeTrackingRelevantFields)
            .Select(c => c.Id)
            .ToListAsync().ConfigureAwait(false);
        var cars = bleDataCarIds
            .Select(carId => settings.Cars.FirstOrDefault(c => c.Id == carId))
            .Where(car => car != default && !string.IsNullOrEmpty(car.Vin))
            .Cast<DtoCar>()
            .ToList();
        //A car that left BLE data collection must not keep a stale uncertain state that would suppress its charging
        //commands forever.
        blePresenceStateService.RetainOnly(cars.Select(c => c.Id).ToList());
        //Phase 1: one presence request per configured container and adapter. A presence request never waits for the
        //adapter, so these can all run at once; they touch neither the DbContext nor the error list.
        var configuredGroups = cars
            .GroupBy(c => (Host: NormalizeBleHost(c.BleApiBaseUrl), Adapter: NormalizeBleAdapter(c.BleAdapterAddress)))
            .ToList();
        var maxAge = TimeSpan.FromSeconds(configurationWrapper.BlePresenceMaxAgeSeconds());
        var groupPresences = await Task.WhenAll(configuredGroups
            .Select(group => FetchGroupPresence(group.Key.Host, group.Key.Adapter, group.ToList(), maxAge))).ConfigureAwait(false);
        //Phase 2: the configured adapter does not tell which radio a car uses - no adapter selected means the
        //container's default one, which may well be the adapter another car selected explicitly. Only the adapter the
        //container resolved does, and only groups on different radios may read in parallel: reads on one radio queue
        //on its worker in the container, where a read waiting behind a slow connect runs out of TSC's HTTP timeout
        //and reports a healthy car as failing.
        var radioGroups = groupPresences
            .GroupBy(RadioGroupKey(groupPresences))
            .ToList();
        //A DbContext must never be used by two operations at once, so only the first radio group runs on this
        //instance's own scope and every further one gets a scope of its own.
        var refreshes = radioGroups.Select((radioGroup, index) => index == 0
            ? RefreshRadioGroupSafely(radioGroup.ToList())
            : RefreshRadioGroupInOwnScope(radioGroup.ToList()));
        await Task.WhenAll(refreshes).ConfigureAwait(false);
    }

    private async Task<DtoBleGroupPresence> FetchGroupPresence(string? host, string? adapter, List<DtoCar> cars, TimeSpan maxAge)
    {
        var vins = cars.Select(c => c.Vin!).ToList();
        try
        {
            //keepWarmSeconds is only ever sent here, on the scheduled poll: the worker of this adapter stays warm
            //between polls - and with it its background scan - while one-off commands never change the warm window.
            var presence = await bleService.GetPresence(host, adapter, vins, BleConstants.BleKeepWarmSeconds,
                (int)maxAge.TotalSeconds).ConfigureAwait(false);
            return new DtoBleGroupPresence { Host = host, Adapter = adapter, Cars = cars, Presence = presence, };
        }
        catch (Exception ex)
        {
            return new DtoBleGroupPresence { Host = host, Adapter = adapter, Cars = cars, PresenceException = ex, };
        }
    }

    /// <summary>
    /// The adapter the container resolved for this group, or null when the answer does not say: the request failed,
    /// the container reported an error or it is too old to report the adapter.
    /// </summary>
    internal static string? ResolvedAdapter(DtoBleGroupPresence group) =>
        group.PresenceException == default && string.IsNullOrEmpty(group.Presence?.ErrorMessage)
            ? NormalizeBleAdapter(group.Presence?.Adapter)
            : null;

    /// <summary>
    /// Groups that use the same radio get the same key. When the radio of any group on a container is unknown, every
    /// group on that container shares one key: serializing needlessly only costs time, reading one radio in parallel
    /// costs false errors.
    /// </summary>
    internal static Func<DtoBleGroupPresence, string> RadioGroupKey(IReadOnlyCollection<DtoBleGroupPresence> groups)
    {
        var hostsWithUnknownRadio = groups
            .Where(g => ResolvedAdapter(g) == default)
            .Select(g => g.Host)
            .ToHashSet();
        return group => hostsWithUnknownRadio.Contains(group.Host)
            ? RadioKey(group.Host, null)
            : RadioKey(group.Host, ResolvedAdapter(group));
    }

    /// <summary>
    /// Identifies one radio. A Bluetooth address belongs to one physical adapter, so it alone is the key: the same
    /// container reached once by DNS name and once by IP address is still one radio. Other adapter keys of the
    /// container ("hci:hci0" when the address is unknown, "default" without adapter enumeration) are only unique on
    /// their own container and keep the host.
    /// </summary>
    internal static string RadioKey(string? host, string? adapter) =>
        adapter != default && BluetoothAddressRegex.IsMatch(adapter) ? adapter : $"{host}|{adapter}";

    private static readonly Regex BluetoothAddressRegex = new("^([0-9A-F]{2}:){5}[0-9A-F]{2}$", RegexOptions.Compiled);

    /// <summary>
    /// The same container is often entered once with and once without a trailing slash; both must end up in the same
    /// group as they are served by the same workers.
    /// </summary>
    internal static string? NormalizeBleHost(string? host)
    {
        var normalized = host?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    internal static string? NormalizeBleAdapter(string? adapter)
    {
        var normalized = adapter?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private async Task RefreshRadioGroupInOwnScope(List<DtoBleGroupPresence> groups)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var scopedService = scope.ServiceProvider.GetRequiredService<IBleVehicleDataService>();
            await scopedService.RefreshRadioGroupSafely(groups).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not refresh BLE data of the cars on {host} in their own scope", groups.FirstOrDefault()?.Host);
        }
    }

    public async Task RefreshRadioGroupSafely(List<DtoBleGroupPresence> groups)
    {
        //All these groups use one radio, so they run one after the other.
        foreach (var group in groups)
        {
            try
            {
                await RefreshGroup(group).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                //One failing group must neither stop the others nor fail the whole job.
                logger.LogError(ex, "Error while refreshing BLE data of the cars on {host} (adapter {adapter})", group.Host, group.Adapter);
            }
        }
    }

    private async Task RefreshGroup(DtoBleGroupPresence group)
    {
        var (host, adapter, cars) = (group.Host, group.Adapter, group.Cars);
        logger.LogTrace("{method}({host}, {adapter}, {carCount} cars)", nameof(RefreshGroup), host, adapter, cars.Count);
        var maxAge = TimeSpan.FromSeconds(configurationWrapper.BlePresenceMaxAgeSeconds());
        if (group.PresenceException is { } ex)
        {
            logger.LogError(ex, "Presence request for {host} (adapter {adapter}) failed", host, adapter);
            await HandleScanUnavailable(cars, adapter, $"BLE presence request failed: {ex.Message}", isAdapterMissing: false).ConfigureAwait(false);
            return;
        }
        var presence = group.Presence!;
        if (!string.IsNullOrEmpty(presence.ErrorMessage))
        {
            //The container could not answer: this carries no presence information for any car, so the last known
            //state stays valid and only the error is surfaced.
            logger.LogError("Presence request for {host} (adapter {adapter}) could not run: {message}",
                host, adapter, presence.ErrorMessage);
            await HandleScanUnavailable(cars, adapter, $"BLE presence could not be determined: {presence.ErrorMessage}",
                isAdapterMissing: presence.ErrorMessage.Contains("not present on this host", StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);
            return;
        }
        //Tracked per radio: a car without a selected adapter and one that selected the default adapter explicitly
        //must not keep two silence timers for the same radio.
        LogRadioState(host, adapter, presence);
        await HandleRadioEvidence(host, ResolvedAdapter(group) ?? adapter, cars, presence).ConfigureAwait(false);
        foreach (var car in cars)
        {
            var vehicle = presence.Vehicles
                .FirstOrDefault(v => string.Equals(v.Vin, car.Vin, StringComparison.OrdinalIgnoreCase));
            var evidence = EvaluateEvidence(presence, vehicle, maxAge);
            LogVehicleEvidence(car, vehicle);
            RecordPresenceObservation(car, adapter, presence, vehicle, evidence.Age <= maxAge);
            await RefreshCarFromPresence(car, evidence, maxAge).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Everything the container reported about the radio itself. Without it a log can not tell a radio that hears
    /// nothing at all apart from one that hears everything but the car, or a scan that is warming up from one that
    /// is not running.
    /// </summary>
    private void LogRadioState(string? host, string? adapter, DtoBlePresenceResult presence)
    {
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }
        logger.LogTrace("BLE radio {resolvedAdapter} on {host} (requested adapter {adapter}): scanner running {scannerRunning}, " +
                        "warming up {warmingUp}, observing for {observing}, last advertisement of any device {lastAdvertisement} ago, " +
                        "{advertisementsSeen} advertisements ({advertisementsPerSecond}/s) from {distinctDevices} devices, " +
                        "last scan error {lastScanError}, cars heard: {trackedCars}",
            presence.Adapter, host, adapter, presence.ScannerRunning, presence.WarmingUp,
            FormatDuration(TimeSpan.FromMilliseconds(presence.ObservingMs)), FormatMsAgo(presence.LastAdvertisementMsAgo),
            presence.AdvertisementsSeen, presence.AdvertisementsPerSecond, presence.DistinctDevicesSeen,
            presence.LastScanError ?? "none",
            presence.Tracked.Count == 0
                ? "none"
                : string.Join(", ", presence.Tracked.Select(t => $"{t.LocalName} {FormatMsAgo(t.LastSeenMsAgo)} ago via {t.LastSource ?? "unknown"}")));
    }

    private void LogVehicleEvidence(DtoCar car, DtoBlePresenceVehicle? vehicle)
    {
        if (vehicle == default)
        {
            logger.LogTrace("The BLE container did not report car {vin} at all", car.Vin);
            return;
        }
        logger.LogTrace("BLE presence evidence of car {vin} ({localName}): last seen {lastSeen} ago via {source}, " +
                        "last advertisement {lastAdvertisement} ago, last answered command {lastCommand} ago, first heard {firstHeard} ago, " +
                        "RSSI {rssi}, address {address}, {count} advertisements ({namedCount} named)",
            car.Vin, vehicle.LocalName, FormatMsAgo(vehicle.LastSeenMsAgo), vehicle.LastSource ?? "unknown",
            FormatMsAgo(vehicle.LastAdvertisementMsAgo), FormatMsAgo(vehicle.LastCommandSuccessMsAgo),
            FormatMsAgo(vehicle.FirstHeardMsAgo), vehicle.Rssi, vehicle.Address, vehicle.Count, vehicle.NamedCount);
    }

    internal static string FormatMsAgo(long? msAgo) =>
        msAgo is { } milliseconds ? FormatDuration(TimeSpan.FromMilliseconds(milliseconds)) : "never";

    /// <summary>Whole seconds, so hours of silence read as 14:03:12 rather than as a 20 digit tick count.</summary>
    internal static string FormatDuration(TimeSpan duration) =>
        TimeSpan.FromSeconds(Math.Round(duration.TotalSeconds)).ToString("c");

    /// <summary>
    /// How old the newest evidence about a car is, or null when nothing may be concluded from this answer.
    ///
    /// Evidence within the max age proves the car is here however long the scan has been observing: a car heard
    /// milliseconds ago is present whether or not the container calls itself warmed up. The warm-up and scanner flags
    /// only gate the negative conclusion, because right after a container or worker restart every car reads as long
    /// unheard and that is ignorance, not absence.
    ///
    /// Reading the flags before the evidence is what made a car unreachable in blocks: the deaf adapter watchdog
    /// restarted the worker every few minutes, and each restart threw away fresh advertisements for a full max age.
    /// </summary>
    internal static TimeSpan? EvidenceAge(DtoBlePresenceResult presence, DtoBlePresenceVehicle? vehicle, TimeSpan maxAge) =>
        EvaluateEvidence(presence, vehicle, maxAge).Age;

    /// <summary>
    /// <see cref="EvidenceAge"/> plus, when it is null, why nothing may be concluded. The reason is for the log only:
    /// "nothing known" looks the same for a scan that warms up for a minute and a radio that never heard the car at
    /// all, and only the second one is a fault.
    /// </summary>
    internal static BleEvidence EvaluateEvidence(DtoBlePresenceResult presence, DtoBlePresenceVehicle? vehicle, TimeSpan maxAge)
    {
        if (vehicle == default)
        {
            return new BleEvidence(null, "the BLE container did not report the car at all");
        }
        if (vehicle.LastSeenMsAgo is not { } lastSeen)
        {
            return new BleEvidence(null, "the BLE container never heard the car since it started, neither by advertisement nor by an answered command");
        }
        var age = TimeSpan.FromMilliseconds(lastSeen);
        if (age <= maxAge)
        {
            return new BleEvidence(age, null);
        }
        if (!presence.ScannerRunning)
        {
            var scanError = string.IsNullOrEmpty(presence.LastScanError) ? string.Empty : $" (last scan error: {presence.LastScanError})";
            return new BleEvidence(null, $"the car was last heard {FormatDuration(age)} ago and the BLE scan is not running{scanError}");
        }
        if (presence.WarmingUp)
        {
            return new BleEvidence(null, $"the car was last heard {FormatDuration(age)} ago and the BLE scan is still warming up " +
                                         $"(observing for {FormatDuration(TimeSpan.FromMilliseconds(presence.ObservingMs))})");
        }
        return new BleEvidence(age, null);
    }

    /// <summary>The age of the newest evidence about a car, or why nothing may be concluded when there is none.</summary>
    internal readonly record struct BleEvidence(TimeSpan? Age, string? UnknownReason);

    /// <summary>
    /// Keeps what was known about a car at this poll for later inspection. Only presence drives behaviour, but the
    /// rest is what tells an unreliable link apart from an absent car, so it is recorded rather than dropped here.
    /// </summary>
    private void RecordPresenceObservation(DtoCar car, string? adapter, DtoBlePresenceResult presence,
        DtoBlePresenceVehicle? vehicle, bool isPresent)
    {
        blePresenceStateService.RegisterObservation(car.Id, new DtoBleBeaconObservation
        {
            Timestamp = new DateTimeOffset(dateTimeProvider.UtcNow(), TimeSpan.Zero),
            IsPresent = isPresent,
            LastSeenMsAgo = vehicle?.LastSeenMsAgo,
            EvidenceSource = vehicle?.LastSource,
            Rssi = vehicle?.Rssi,
            AdvertisementsSeen = presence.AdvertisementsSeen,
            Adapter = adapter,
        });
    }

    /// <summary>
    /// Applies one presence answer to a car and publishes the resulting state. Coordinated per car so the scheduled
    /// refresh and an on demand single car read can never talk to the same car at the same time.
    ///
    /// A car that is not present is not talked to at all: that is the whole point of asking first. The old design
    /// paid a scan window, and a command only design would pay a full connect timeout, for a car that is simply gone.
    /// </summary>
    private async Task RefreshCarFromPresence(DtoCar car, BleEvidence evidence, TimeSpan maxAge)
    {
        if (!bleReadCoordinator.TryBeginRead(car.Id))
        {
            return;
        }
        try
        {
            try
            {
                var decision = blePresenceStateService.RegisterPresenceAge(car.Id, evidence.Age, maxAge);
                if (decision == BlePresenceDecision.Present)
                {
                    await RefreshPresentCarData(car).ConfigureAwait(false);
                }
                else
                {
                    await HandleAbsentCar(car, decision, evidence.UnknownReason).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while refreshing BLE data for car {vin}", car.Vin);
                await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                    $"Error while getting vehicle data via BLE for car {car.Vin}", ex.Message,
                    issueKeys.BleDataCollectionError, car.Vin, ex.StackTrace).ConfigureAwait(false);
                return;
            }
            try
            {
                await loadPointManagementService.CarStateChanged(car.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing CarStateChanged for car ID {carId}", car.Id);
            }
        }
        finally
        {
            bleReadCoordinator.EndRead(car.Id);
        }
    }

    public async Task RefreshSingleCarData(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(RefreshSingleCarData), carId);
        if (!configurationWrapper.GetVehicleDataViaBle() || !configurationWrapper.GetVehicleDataFromTesla())
        {
            return;
        }
        var isBleDataCar = await teslaSolarChargerContext.Cars
            .Where(c => (c.Id == carId)
                        && (c.ShouldBeManaged == true)
                        && (c.CarType == CarType.Tesla)
                        && c.UseBle
                        && !c.UseFleetTelemetry
                        && !c.IncludeTrackingRelevantFields)
            .AnyAsync().ConfigureAwait(false);
        if (!isBleDataCar)
        {
            return;
        }
        var car = settings.Cars.FirstOrDefault(c => c.Id == carId);
        if (car == default || string.IsNullOrEmpty(car.Vin))
        {
            return;
        }
        var maxAge = TimeSpan.FromSeconds(configurationWrapper.BlePresenceMaxAgeSeconds());
        DtoBlePresenceResult presence;
        try
        {
            //No keepWarmSeconds: only the scheduled poll owns the container's warm window.
            presence = await bleService.GetPresence(car.BleApiBaseUrl, car.BleAdapterAddress,
                new List<string> { car.Vin }, null, (int)maxAge.TotalSeconds).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Presence request for single car {vin} failed", car.Vin);
            return;
        }
        if (!string.IsNullOrEmpty(presence.ErrorMessage))
        {
            //Carries no presence information: leave the state to the scheduled refresh.
            logger.LogDebug("Presence for single car {vin} could not be determined: {message}", car.Vin, presence.ErrorMessage);
            return;
        }
        var vehicle = presence.Vehicles
            .FirstOrDefault(v => string.Equals(v.Vin, car.Vin, StringComparison.OrdinalIgnoreCase));
        LogVehicleEvidence(car, vehicle);
        await RefreshCarFromPresence(car, EvaluateEvidence(presence, vehicle, maxAge), maxAge).ConfigureAwait(false);
    }

    private async Task HandleScanUnavailable(List<DtoCar> cars, string? adapter, string message, bool isAdapterMissing)
    {
        foreach (var car in cars)
        {
            if (isAdapterMissing)
            {
                //Never a silent fallback to a different radio: a missing configured adapter is an explicit error.
                await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                    $"Configured Bluetooth adapter for car {car.Vin} not found",
                    $"The adapter {adapter} is not present on the BLE container's host. Check the adapter selection of the car or replug the adapter.",
                    issueKeys.BleAdapterNotFound, car.Vin, null).ConfigureAwait(false);
                continue;
            }
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                $"Error while getting vehicle data via BLE for car {car.Vin}", message,
                issueKeys.BleDataCollectionError, car.Vin, null).ConfigureAwait(false);
        }
    }

    private async Task HandleRadioEvidence(string? host, string? adapter, List<DtoCar> cars, DtoBlePresenceResult presence)
    {
        //Whether the radio received anything at all recently, from any device. Independent of whether a car was
        //heard: that is what tells a dead radio apart from an empty driveway.
        var heardAnything = presence.LastAdvertisementMsAgo is { } lastAdvertisement
                            && lastAdvertisement <= presence.MaxAgeMs;
        var silence = blePresenceStateService.RegisterRadioEvidence(RadioKey(host, adapter), heardAnything, new DateTimeOffset(dateTimeProvider.UtcNow(), TimeSpan.Zero));
        if (!heardAnything)
        {
            //Counted from the last poll on which TSC saw the radio hear anything, or from TSC's start.
            logger.LogTrace("BLE radio {radio} heard no advertisement of any device within the max age, silent for {silence} as seen by TSC",
                RadioKey(host, adapter), FormatDuration(silence));
        }
        foreach (var car in cars)
        {
            if (heardAnything)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.BleRadioSilence, car.Vin).ConfigureAwait(false);
            }
            else if (silence > RadioSilenceWarningDuration)
            {
                //Diagnostics only, presence is never touched: at a site with no other Bluetooth devices this is
                //expected while the car is away, but after the site B incident (a starved radio reported a garaged
                //car as away for days) a long fully silent radio is worth a hint.
                await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                    $"BLE radio of the container used for car {car.Vin} hears nothing",
                    $"No Bluetooth advertisement of any device was received for {silence.TotalHours:0} hours. If other Bluetooth devices are usually nearby, " +
                    "check the radio: on a Raspberry Pi a poor WiFi link starves Bluetooth because they share one antenna (see the README), " +
                    "or use a USB Bluetooth adapter. If no Bluetooth devices are ever near the container, this message is expected while the car is away.",
                    issueKeys.BleRadioSilence, car.Vin, null).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// A car that is not present is never talked to: no connect, no command, no timeout. That is the point of asking
    /// the container first, and it is what an absent car used to cost a scan window for.
    /// </summary>
    private async Task HandleAbsentCar(DtoCar car, BlePresenceDecision decision, string? unknownReason)
    {
        var vin = car.Vin!;
        var timestamp = dateTimeProvider.UtcNow();
        switch (decision)
        {
            case BlePresenceDecision.JustConfirmedAway:
                logger.LogInformation("Car {vin} has not been heard for the whole confirmation duration, car is confirmed as away", vin);
                UpdateHomePresence(car, false, timestamp);
                UpdateOnlineState(car, false, timestamp);
                ResetChargingValuesForAwayCar(car, timestamp);
                await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                break;
            case BlePresenceDecision.AlreadyAway:
                //The car is already marked as away: nothing changed, so do not write the same values again.
                break;
            case BlePresenceDecision.Unknown:
                //The container cannot say yet, e.g. its scan is still warming up after a restart. Ignorance is not
                //absence: keep the last known state and wait.
                logger.LogDebug("Nothing is known about car {vin} yet, keeping last known state", vin);
                logger.LogTrace("Presence of car {vin} is unknown because {reason}", vin, unknownReason ?? "no reason was reported");
                break;
            default:
                //Not silent long enough yet: keep the last known state; charging commands are suspended via
                //IsPresenceUncertain until either the car is heard again or the away state is confirmed.
                logger.LogDebug("Car {vin} not heard, keeping last known state until the away state is confirmed", vin);
                break;
        }
        //Getting here means the container answered and the car was not read at all, so nothing can currently be wrong
        //with its data collection. Resolving only on the away transition left an error raised while the car was
        //already gone - a presence request that timed out because the container dropped off the network, say - open
        //until the car came back and was read successfully, which can be days.
        await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
    }

    /// <summary>
    /// A car that is confirmed as away can not be plugged in at home anymore: reset the charging values that would
    /// otherwise stay stale until the car is back in BLE range and awake. The state of charge intentionally keeps its
    /// last known value. The values are inferred rather than read from the car, so they are stored with
    /// CarValueSource.Estimation.
    /// </summary>
    private void ResetChargingValuesForAwayCar(DtoCar car, DateTime timestamp)
    {
        AddBooleanValue(car, CarValueType.IsPluggedIn, false, timestamp, CarValueSource.Estimation);
        AddBooleanValue(car, CarValueType.IsCharging, false, timestamp, CarValueSource.Estimation);
        AddIntValue(car, CarValueType.ChargeAmps, 0, timestamp, CarValueSource.Estimation, skipDefaultValue: false);
        //The car left home, so any sleep window it was in is meaningless. The stability period starts fresh when it
        //comes back.
        bleSleepWindowService.ResetSleepWindow(car.Id);
    }

    /// <summary>
    /// Refreshes the data of a car whose beacon was just seen. Presence is decided by the beacon scan alone: a
    /// failed read directly after a beacon hit is a transient failure of a provably present car and must never count
    /// towards the away confirmation.
    /// </summary>
    private async Task RefreshPresentCarData(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(RefreshPresentCarData), car.Vin);
        var vin = car.Vin!;
        var timestamp = dateTimeProvider.UtcNow();
        //Persist the presence right away in case the body controller read afterwards fails.
        UpdateHomePresence(car, true, timestamp);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        if (bleAccessGateService.IsPairingInProgress(car.BleApiBaseUrl))
        {
            //Adding a key owns that container's adapter, so reading now could only time out - and it would take the
            //radio away from the pairing the user is standing at their car for.
            logger.LogDebug("Skipping the BLE read of car {vin} while a key is being added on its container", vin);
            return;
        }
        if (bleAccessGateService.IsKeyRejected(car.Id))
        {
            //The car answers every request with the same rejection, and the poll runs every few seconds: asking
            //anyway fills the radio and the error list with the same answer instead of the one thing the user has to
            //do about it.
            logger.LogDebug("Skipping the BLE read of car {vin}: the car rejects TSC's key", vin);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}", BleAccessGateService.KeyNotPairedMessage,
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }
        var bodyControllerStateResult = await bleService.GetBodyControllerState(vin).ConfigureAwait(false);
        timestamp = dateTimeProvider.UtcNow();
        if (!bodyControllerStateResult.Success)
        {
            logger.LogError("Could not get body controller state for car {vin}: {outcome} {resultMessage}", vin,
                bodyControllerStateResult.Outcome, bodyControllerStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not get body controller state: {bodyControllerStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }

        var bodyControllerState = DeserializeBodyControllerState(bodyControllerStateResult.ResultMessage);
        if (bodyControllerState == default)
        {
            logger.LogError("Could not parse body controller state for car {vin}: {resultMessage}", vin, bodyControllerStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not parse body controller state: {bodyControllerStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }

        var isAwake = bodyControllerState.VehicleSleepStatus == VehicleSleepStatus.VehicleSleepStatusAwake;
        UpdateOnlineState(car, isAwake, timestamp);
        if (!isAwake)
        {
            //Do not get the charge state of sleeping cars as requests to the infotainment system would wake up the
            //car. The last known charge state values stay valid until the car is awake again.
            bleSleepWindowService.NotifyAsleep(car.Id);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
            return;
        }

        //Requests to the infotainment system reset the car's standby timer and keep it awake (verified on a real car:
        //the VCSEC body controller poll alone does not, the infotainment charge state poll does). Inside a BLE sleep
        //window the infotainment poll is therefore withheld so the standby timer can run out.
        var windowMinutes = configurationWrapper.BleSleepWindowMinutes();
        var stabilityMinutes = configurationWrapper.BleSleepStabilityMinutes();
        //A charging car is definitely awake and not trying to sleep, so it must never enter a sleep window: nothing in
        //the tracked signature changes while it charges steadily, so it would otherwise be silenced after the
        //stability period and TSC would stop seeing the charge state. The sleep window reset on charge commands does
        //not cover this, as TeslaFleetApiService.SetAmp sends nothing while the target current is unchanged.
        var isCharging = car.IsCharging.Value == true;
        if (isCharging)
        {
            bleSleepWindowService.ResetSleepWindow(car.Id);
        }
        else if (!bleSleepWindowService.ShouldPollInfotainment(car.Id, timestamp, windowMinutes))
        {
            logger.LogDebug("Car {vin} is in a BLE sleep window, skip infotainment charge state poll", vin);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
            return;
        }
        var chargeStateResult = await bleService.GetChargeState(vin).ConfigureAwait(false);
        if (!chargeStateResult.Success)
        {
            logger.LogError("Could not get charge state for car {vin}: {outcome} {resultMessage}", vin,
                chargeStateResult.Outcome, chargeStateResult.ResultMessage);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not get charge state: {chargeStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }
        var chargeState = DeserializeChargeState(chargeStateResult.ResultMessage);
        if (chargeState == default)
        {
            logger.LogError("Could not parse charge state for car {vin}: {resultMessage}", vin, chargeStateResult.ResultMessage);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not parse charge state: {chargeStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }
        UpdateChargeStateValues(car, chargeState, timestamp);
        if (!isCharging)
        {
            //Feed the fresh full poll into the sleep window state machine so it can (re-)start a window once the car
            //has been idle and closed up long enough. Skipped while charging so no window can form (see above); the
            //stability period starts fresh on the first poll after charging stopped.
            bleSleepWindowService.ObserveFullPoll(car.Id, bodyControllerState, DerivePluggedIn(chargeState),
                chargeState.ChargeLimitSoc, timestamp, windowMinutes, stabilityMinutes);
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
    }

    /// <summary>
    /// The charging state the car reported, or <see cref="ChargingStateCase.None"/> when it reported none. Tesla
    /// models this as a oneof of empty messages, so the set case is the value.
    /// </summary>
    private static ChargingStateCase ChargingState(ChargeState chargeState) =>
        chargeState.ChargingState?.TypeCase ?? ChargingStateCase.None;

    /// <summary>
    /// Derives the plugged in state from the BLE charging state, or null if it is unknown/not reported.
    /// </summary>
    internal static bool? DerivePluggedIn(ChargeState chargeState)
    {
        var chargingState = ChargingState(chargeState);
        if (chargingState is ChargingStateCase.None or ChargingStateCase.Unknown)
        {
            return null;
        }
        return chargingState != ChargingStateCase.Disconnected;
    }

    internal void UpdateChargeStateValues(DtoCar car, ChargeState chargeState, DateTime timestamp)
    {
        //Every one of these is an optional proto3 field, so a reported 0 (e.g. 0 A while plugged in but not charging)
        //is distinguishable from a value the car did not send at all.
        AddIntValue(car, CarValueType.StateOfCharge, Reported(chargeState.HasBatteryLevel, chargeState.BatteryLevel), timestamp);
        AddIntValue(car, CarValueType.StateOfChargeLimit, Reported(chargeState.HasChargeLimitSoc, chargeState.ChargeLimitSoc), timestamp);
        AddIntValue(car, CarValueType.ChargerVoltage, Reported(chargeState.HasChargerVoltage, chargeState.ChargerVoltage), timestamp);
        AddIntValue(car, CarValueType.ChargeAmps, Reported(chargeState.HasChargerActualCurrent, chargeState.ChargerActualCurrent), timestamp);
        AddIntValue(car, CarValueType.ChargerPhases, Reported(chargeState.HasChargerPhases, chargeState.ChargerPhases), timestamp);
        AddIntValue(car, CarValueType.ChargeCurrentRequest, Reported(chargeState.HasChargeCurrentRequest, chargeState.ChargeCurrentRequest), timestamp);
        AddIntValue(car, CarValueType.ChargerPilotCurrent, Reported(chargeState.HasChargerPilotCurrent, chargeState.ChargerPilotCurrent), timestamp);
        var chargingState = ChargingState(chargeState);
        if (chargingState is not (ChargingStateCase.None or ChargingStateCase.Unknown))
        {
            AddBooleanValue(car, CarValueType.IsPluggedIn, chargingState != ChargingStateCase.Disconnected, timestamp);
            AddBooleanValue(car, CarValueType.IsCharging, chargingState == ChargingStateCase.Charging, timestamp);
        }
    }

    private static int? Reported(bool hasValue, int value) => hasValue ? value : null;

    private void UpdateHomePresence(DtoCar car, bool isAtHome, DateTime timestamp)
    {
        AddBooleanValue(car, CarValueType.LocatedAtHome, isAtHome, timestamp);
        car.IsHomeGeofence.Update(new DateTimeOffset(timestamp, TimeSpan.Zero), isAtHome);
    }

    private void UpdateOnlineState(DtoCar car, bool isOnline, DateTime timestamp)
    {
        AddBooleanValue(car, CarValueType.AsleepOrOffline, !isOnline, timestamp);
        car.IsOnline.Update(new DateTimeOffset(timestamp, TimeSpan.Zero), isOnline);
    }

    private void AddIntValue(DtoCar car, CarValueType type, int? value, DateTime timestamp,
        CarValueSource source = CarValueSource.Ble, bool skipDefaultValue = true)
    {
        if (skipDefaultValue && value == default)
        {
            return;
        }
        var carValueLog = new CarValueLog
        {
            CarId = car.Id,
            Timestamp = timestamp,
            Type = type,
            Source = source,
            IntValue = value,
        };
        teslaSolarChargerContext.CarValueLogs.Add(carValueLog);
        carPropertyUpdateHelper.UpdateDtoCarProperty(car, carValueLog);
    }

    private void AddBooleanValue(DtoCar car, CarValueType type, bool value, DateTime timestamp,
        CarValueSource source = CarValueSource.Ble)
    {
        var carValueLog = new CarValueLog
        {
            CarId = car.Id,
            Timestamp = timestamp,
            Type = type,
            Source = source,
            BooleanValue = value,
        };
        teslaSolarChargerContext.CarValueLogs.Add(carValueLog);
        carPropertyUpdateHelper.UpdateDtoCarProperty(car, carValueLog);
    }

    internal static VehicleStatus? DeserializeBodyControllerState(string? resultMessage) =>
        BleProtoJson.TryParse<VehicleStatus>(resultMessage);

    internal static ChargeState? DeserializeChargeState(string? resultMessage)
    {
        //`tesla-control state charge` wraps its answer in a VehicleData message. Unknown fields are ignored, so a bare
        //ChargeState would also parse as a VehicleData - just an empty one, which is why the wrapper is tried first
        //and the bare form only used when no charge state came out of it.
        var vehicleData = BleProtoJson.TryParse<VehicleData>(resultMessage);
        if (vehicleData?.ChargeState != default)
        {
            return vehicleData.ChargeState;
        }
        return BleProtoJson.TryParse<ChargeState>(resultMessage);
    }
}
