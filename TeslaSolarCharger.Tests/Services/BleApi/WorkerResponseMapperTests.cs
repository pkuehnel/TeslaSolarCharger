using PkSoftwareService.Custom.Backend.Ble;
using System.Linq;
using TeslaSolarCharger.BleApi.Services;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.BleApi;

/// <summary>
/// The mapping between the Go worker's line protocol and the shared DTOs decides whether a car is reported as absent,
/// so a silent mismatch here would reintroduce the "car in the garage reported as not at home" defect.
/// </summary>
public class WorkerResponseMapperTests
{
    [Theory]
    [InlineData("ok", BleCommandOutcome.Ok)]
    [InlineData("carAbsent", BleCommandOutcome.CarAbsent)]
    [InlineData("linkFailed", BleCommandOutcome.LinkFailed)]
    [InlineData("carAsleep", BleCommandOutcome.CarAsleep)]
    [InlineData("carRefused", BleCommandOutcome.CarRefused)]
    [InlineData("adapterUnavailable", BleCommandOutcome.AdapterUnavailable)]
    [InlineData("invalidRequest", BleCommandOutcome.InvalidRequest)]
    public void MapsEveryOutcomeTheWorkerCanSend(string workerOutcome, BleCommandOutcome expected)
    {
        Assert.Equal(expected, WorkerResponseMapper.MapOutcome(workerOutcome));
    }

    [Theory]
    [InlineData("somethingNew")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownOutcomeBecomesWorkerError(string? workerOutcome)
    {
        //WorkerError carries no presence information, which is the safe default for a worker that is newer than the
        //container or answered with garbage.
        Assert.Equal(BleCommandOutcome.WorkerError, WorkerResponseMapper.MapOutcome(workerOutcome));
    }

    [Fact]
    public void ParsesASuccessfulCommandResult()
    {
        const string line = """
            {"kind":"result","id":7,"ok":true,"outcome":"ok","result":{"vehicleSleepStatus":"VEHICLE_SLEEP_STATUS_AWAKE"},
             "scan":{"beaconFound":true,"rssi":-63,"otherAdvertisementsSeen":41,"distinctDevicesSeen":8,"scanDurationMs":52},
             "durationMs":835,"connectMs":510,"timestampUtc":"2026-07-29T09:00:00.123Z"}
            """;
        var response = WorkerResponseMapper.ParseLine(line);
        Assert.NotNull(response);
        var result = WorkerResponseMapper.ToCommandResult(response);

        Assert.True(result.Success);
        Assert.Equal(BleCommandOutcome.Ok, result.Outcome);
        Assert.Null(result.ErrorType);
        //The payload travels where the stdout of tesla-control used to be, so the server side parsers keep working.
        Assert.Contains("VEHICLE_SLEEP_STATUS_AWAKE", result.ResultMessage);
        Assert.True(result.BeaconFound);
        Assert.Equal(41, result.OtherAdvertisementsSeen);
        Assert.Equal(8, result.DistinctDevicesSeen);
        Assert.Equal(52, result.ScanDurationMs);
        Assert.Equal(510, result.ConnectMs);
        Assert.Equal(835, result.DurationMs);
    }

    [Fact]
    public void ParsesAnAbsentCarResult()
    {
        const string line = """
            {"kind":"result","id":8,"ok":false,"outcome":"carAbsent","phase":"scan",
             "error":"failed to find BLE beacon for VIN: car is not in BLE range (scanned 3000 ms, heard 12 advertisements from 4 other devices)",
             "scan":{"beaconFound":false,"otherAdvertisementsSeen":12,"distinctDevicesSeen":4,"scanDurationMs":3000},
             "durationMs":3010,"connectMs":3005}
            """;
        var result = WorkerResponseMapper.ToCommandResult(WorkerResponseMapper.ParseLine(line)!);

        Assert.False(result.Success);
        Assert.Equal(BleCommandOutcome.CarAbsent, result.Outcome);
        Assert.Equal(BleCommandPhase.Scan, result.Phase);
        Assert.False(result.BeaconFound);
        //Legacy field for BLE containers/servers of the previous generation.
        Assert.Equal(ErrorType.TeslaControl, result.ErrorType);
    }

    [Fact]
    public void CarRefusalKeepsTheReasonAndMapsToTheLegacyCarExecutionType()
    {
        const string line = """
            {"kind":"result","id":9,"ok":false,"outcome":"carRefused","phase":"command",
             "error":"car could not execute command: is_charging","carErrorMessage":"is_charging"}
            """;
        var result = WorkerResponseMapper.ToCommandResult(WorkerResponseMapper.ParseLine(line)!);

        Assert.Equal(BleCommandOutcome.CarRefused, result.Outcome);
        Assert.Equal("is_charging", result.CarErrorMessage);
        //TeslaFleetApiService treats a refusal of charging-start while already charging as success, and older
        //versions of that check only know ErrorType.
        Assert.Equal(ErrorType.CarExecution, result.ErrorType);
    }

    [Fact]
    public void ParsesAMultiVinBeaconScanResult()
    {
        const string line = """
            {"kind":"result","id":1,"ok":true,"outcome":"ok","beaconScan":{"windowMs":3000,"scanDurationMs":612,
             "otherAdvertisementsSeen":57,"distinctDevicesSeen":9,
             "vehicles":[{"vin":"VIN1","beaconFound":true,"rssi":-63,"address":"aa:bb:cc:dd:ee:ff","connectable":true,"foundAfterMs":48},
                         {"vin":"VIN2","beaconFound":false}]}}
            """;
        var result = WorkerResponseMapper.ToBeaconScanResult(WorkerResponseMapper.ParseLine(line)!);

        Assert.True(result.Success);
        Assert.Equal(BleCommandOutcome.Ok, result.Outcome);
        Assert.Equal(3000, result.WindowMs);
        Assert.Equal(612, result.ScanDurationMs);
        Assert.Equal(57, result.OtherAdvertisementsSeen);
        Assert.Equal(9, result.DistinctDevicesSeen);
        Assert.Equal(2, result.Vehicles.Count);
        var found = result.Vehicles.Single(v => v.Vin == "VIN1");
        Assert.True(found.BeaconFound);
        Assert.Equal(-63, found.Rssi);
        Assert.Equal(48, found.FoundAfterMs);
        Assert.False(result.Vehicles.Single(v => v.Vin == "VIN2").BeaconFound);
    }

    [Fact]
    public void ParsesTheBackgroundScannerState()
    {
        const string line = """
            {"kind":"result","id":4,"ok":true,"outcome":"ok","presence":{"scannerRunning":true,"observingMs":600000,
             "scanActiveMs":594000,"pausedMs":6000,"restarts":1,"scanErrors":0,"advertisementsSeen":18000,
             "distinctDevicesSeen":37,"lastAdvertisementMsAgo":312,"maxAgeMs":90000,"scanWhileConnected":true,
             "vehicles":[{"vin":"VIN1","localName":"S0011223344556677C","heard":true,"lastHeardMsAgo":18422,
                          "lastAdvertisementMsAgo":18422,"rssi":-73,"address":"aa:bb:cc:dd:ee:ff","connectable":true,
                          "count":91,"namedCount":37,"addressCount":54,"lastSource":"advertisement",
                          "gapsMs":[400,41000],"medianGapMs":41000,"maxGapMs":41000},
                         {"vin":"VIN2","localName":"S9988776655443322C","heard":false}],
             "tracked":[{"localName":"S0011223344556677C","heard":true,"count":91}]}}
            """;
        var result = WorkerResponseMapper.ToScannerStatus(WorkerResponseMapper.ParseLine(line)!, "hci1");

        Assert.True(result.ScannerRunning);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("hci1", result.Adapter);
        Assert.Equal(99d, result.DutyCyclePercent);
        Assert.Equal(30d, result.AdvertisementsPerSecond);
        Assert.Equal(1, result.Restarts);
        Assert.Equal(312, result.LastAdvertisementMsAgo);
        var heard = result.Vehicles.Single(v => v.Vin == "VIN1");
        Assert.True(heard.Heard);
        Assert.Equal(18422, heard.LastHeardMsAgo);
        //Named against address recognition is what tells a car whose name only travels in the scan response apart
        //from one that advertises its name.
        Assert.Equal(37, heard.NamedCount);
        Assert.Equal(54, heard.AddressCount);
        Assert.Equal(41000, heard.MedianGapMs);
        Assert.False(result.Vehicles.Single(v => v.Vin == "VIN2").Heard);
        Assert.Single(result.Tracked);
    }

    [Fact]
    public void AScannerAnswerWithoutStateCarriesTheError()
    {
        const string line = """{"kind":"result","id":4,"ok":false,"outcome":"adapterUnavailable","error":"adapter is gone"}""";
        var result = WorkerResponseMapper.ToScannerStatus(WorkerResponseMapper.ParseLine(line)!, "hci0");

        Assert.False(result.ScannerRunning);
        Assert.Equal("adapter is gone", result.ErrorMessage);
    }

    [Fact]
    public void MalformedJsonIsNotParsed()
    {
        Assert.Null(WorkerResponseMapper.ParseLine("this is not json"));
        Assert.Null(WorkerResponseMapper.ParseLine("{\"kind\":\"result\","));
    }

    [Fact]
    public void LocalFailuresCarryTheOutcomeAndNoPresenceInformation()
    {
        var result = WorkerResponseMapper.CreateLocalFailure(BleCommandOutcome.AdapterNotFound, "adapter gone");
        Assert.False(result.Success);
        Assert.Equal(BleCommandOutcome.AdapterNotFound, result.Outcome);
        Assert.Null(result.BeaconFound);
        Assert.Equal("adapter gone", result.ResultMessage);
    }

    [Fact]
    public void RequestsSerializeToASingleLineWithCamelCaseNames()
    {
        var payload = WorkerResponseMapper.SerializeRequest(new { id = 3, kind = "beaconScan", vins = new[] { "VIN1" }, windowMs = 3000, });
        Assert.DoesNotContain("\n", payload);
        Assert.Contains("\"windowMs\":3000", payload);
        Assert.Contains("\"kind\":\"beaconScan\"", payload);
    }
}
