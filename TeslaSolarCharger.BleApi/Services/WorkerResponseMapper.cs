using PkSoftwareService.Custom.Backend.Ble;
using System.Text.Json;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Dtos.Worker;

namespace TeslaSolarCharger.BleApi.Services;

/// <summary>
/// Pure mapping between the worker's line protocol and the shared DTOs. Kept free of any I/O so the whole wire
/// contract is unit testable: an unnoticed mismatch here would misclassify results, which is exactly the defect
/// class this rework removes.
/// </summary>
public static class WorkerResponseMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string SerializeRequest(object request) => JsonSerializer.Serialize(request, SerializerOptions);

    public static WorkerResponse? ParseLine(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkerResponse>(line, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Unknown outcome strings map to WorkerError: a worker newer than the container is a bug, and WorkerError
    /// carries no presence information, which is the safe default.
    /// </summary>
    public static BleCommandOutcome MapOutcome(string? outcome) => outcome switch
    {
        "ok" => BleCommandOutcome.Ok,
        "carAbsent" => BleCommandOutcome.CarAbsent,
        "linkFailed" => BleCommandOutcome.LinkFailed,
        "carAsleep" => BleCommandOutcome.CarAsleep,
        "carRefused" => BleCommandOutcome.CarRefused,
        "adapterUnavailable" => BleCommandOutcome.AdapterUnavailable,
        "invalidRequest" => BleCommandOutcome.InvalidRequest,
        _ => BleCommandOutcome.WorkerError,
    };

    public static BleCommandPhase? MapPhase(string? phase) => phase switch
    {
        "scan" => BleCommandPhase.Scan,
        "connect" => BleCommandPhase.Connect,
        "session" => BleCommandPhase.Session,
        "command" => BleCommandPhase.Command,
        _ => null,
    };

    /// <summary>
    /// The legacy ErrorType is only populated for rollout compatibility with servers that predate the outcome field.
    /// </summary>
    public static ErrorType? MapLegacyErrorType(BleCommandOutcome outcome) => outcome switch
    {
        BleCommandOutcome.Ok => null,
        BleCommandOutcome.CarRefused => ErrorType.CarExecution,
        BleCommandOutcome.WorkerError => ErrorType.Exceptional,
        BleCommandOutcome.WorkerTimeout => ErrorType.Exceptional,
        _ => ErrorType.TeslaControl,
    };

    public static DtoBleCommandResult ToCommandResult(WorkerResponse response)
    {
        var outcome = MapOutcome(response.Outcome);
        var result = new DtoBleCommandResult
        {
            Success = response.Ok,
            Outcome = outcome,
            Phase = MapPhase(response.Phase),
            ErrorType = MapLegacyErrorType(outcome),
            CarErrorMessage = string.IsNullOrWhiteSpace(response.CarErrorMessage) ? null : response.CarErrorMessage.Trim(),
            ConnectMs = response.ConnectMs,
            DurationMs = response.DurationMs,
        };
        if (response.Scan != default)
        {
            result.BeaconFound = response.Scan.BeaconFound;
            result.OtherAdvertisementsSeen = response.Scan.OtherAdvertisementsSeen;
            result.DistinctDevicesSeen = response.Scan.DistinctDevicesSeen;
            result.ScanDurationMs = response.Scan.ScanDurationMs;
        }
        if (response.Ok)
        {
            //The result payload (protojson from the vehicle) travels in ResultMessage, exactly where the stdout of
            //tesla-control used to be, so the server side parsers keep working unchanged.
            result.ResultMessage = response.Result is { } payload ? payload.GetRawText() : string.Empty;
        }
        else
        {
            result.ResultMessage = string.IsNullOrWhiteSpace(response.Error)
                ? "BLE worker reported an unknown error"
                : response.Error.Trim();
        }
        return result;
    }

    public static DtoBleBeaconScanResult ToBeaconScanResult(WorkerResponse response)
    {
        var outcome = MapOutcome(response.Outcome);
        var result = new DtoBleBeaconScanResult
        {
            Success = response.Ok,
            Outcome = outcome,
            ResultMessage = response.Ok ? null : response.Error,
        };
        if (response.BeaconScan == default)
        {
            return result;
        }
        result.WindowMs = response.BeaconScan.WindowMs;
        result.ScanDurationMs = response.BeaconScan.ScanDurationMs;
        result.OtherAdvertisementsSeen = response.BeaconScan.OtherAdvertisementsSeen;
        result.DistinctDevicesSeen = response.BeaconScan.DistinctDevicesSeen;
        result.Vehicles = response.BeaconScan.Vehicles.Select(vehicle => new DtoBleBeaconVehicleResult
        {
            Vin = vehicle.Vin,
            BeaconFound = vehicle.BeaconFound,
            Rssi = vehicle.Rssi,
            Address = vehicle.Address,
            Connectable = vehicle.Connectable,
            FoundAfterMs = vehicle.FoundAfterMs,
        }).ToList();
        return result;
    }

    /// <summary>
    /// Maps the worker's view of its background scan onto the container DTO. The derived rates are computed here so
    /// every caller reads the same numbers instead of dividing counters itself.
    /// </summary>
    public static DtoBleScannerStatus ToScannerStatus(WorkerResponse response, string? adapter)
    {
        var result = new DtoBleScannerStatus { Adapter = adapter };
        if (response.Presence == default)
        {
            result.ErrorMessage = string.IsNullOrWhiteSpace(response.Error)
                ? "The BLE worker did not report any scanner state."
                : response.Error.Trim();
            return result;
        }
        var presence = response.Presence;
        result.ScannerRunning = presence.ScannerRunning;
        result.ObservingMs = presence.ObservingMs;
        result.ScanActiveMs = presence.ScanActiveMs;
        result.PausedMs = presence.PausedMs;
        result.DutyCyclePercent = presence.ObservingMs > 0
            ? Math.Round(presence.ScanActiveMs * 100d / presence.ObservingMs, 1)
            : 0;
        result.Restarts = presence.Restarts;
        result.ScanErrors = presence.ScanErrors;
        result.LastScanError = presence.LastScanError;
        result.AdvertisementsSeen = presence.AdvertisementsSeen;
        result.AdvertisementsPerSecond = presence.ObservingMs > 0
            ? Math.Round(presence.AdvertisementsSeen * 1000d / presence.ObservingMs, 2)
            : 0;
        result.DistinctDevicesSeen = presence.DistinctDevicesSeen;
        result.LastAdvertisementMsAgo = presence.LastAdvertisementMsAgo;
        result.MaxAgeMs = presence.MaxAgeMs;
        result.ScanWhileConnected = presence.ScanWhileConnected;
        result.Vehicles = presence.Vehicles.Select(ToScannerVehicle).ToList();
        result.Tracked = presence.Tracked.Select(ToScannerVehicle).ToList();
        if (!response.Ok && !string.IsNullOrWhiteSpace(response.Error))
        {
            result.ErrorMessage = response.Error.Trim();
        }
        return result;
    }

    private static DtoBleScannerVehicle ToScannerVehicle(WorkerPresenceVehicle vehicle) => new()
    {
        Vin = vehicle.Vin,
        LocalName = vehicle.LocalName,
        Heard = vehicle.Heard,
        LastHeardMsAgo = vehicle.LastHeardMsAgo,
        LastAdvertisementMsAgo = vehicle.LastAdvertisementMsAgo,
        FirstHeardMsAgo = vehicle.FirstHeardMsAgo,
        Rssi = vehicle.Rssi,
        Address = vehicle.Address,
        Connectable = vehicle.Connectable,
        Count = vehicle.Count,
        NamedCount = vehicle.NamedCount,
        AddressCount = vehicle.AddressCount,
        LastSource = vehicle.LastSource,
        GapsMs = vehicle.GapsMs,
        MedianGapMs = vehicle.MedianGapMs,
        MaxGapMs = vehicle.MaxGapMs,
    };

    /// <summary>
    /// A failure produced by the container itself (worker crashed, hung or the configured adapter is missing), not
    /// by the worker.
    /// </summary>
    public static DtoBleCommandResult CreateLocalFailure(BleCommandOutcome outcome, string message) => new()
    {
        Success = false,
        Outcome = outcome,
        ErrorType = MapLegacyErrorType(outcome),
        ResultMessage = message,
    };

    public static DtoBleBeaconScanResult CreateLocalScanFailure(BleCommandOutcome outcome, string message) => new()
    {
        Success = false,
        Outcome = outcome,
        ResultMessage = message,
    };
}
