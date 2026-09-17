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
        Assert.Equal(510, result.ConnectMs);
        Assert.Equal(835, result.DurationMs);
    }

    [Fact]
    public void ParsesAnAbsentCarResult()
    {
        const string line = """
            {"kind":"result","id":8,"ok":false,"outcome":"carAbsent","phase":"scan",
             "error":"failed to connect: timed out",
             "durationMs":3010,"connectMs":3005}
            """;
        var result = WorkerResponseMapper.ToCommandResult(WorkerResponseMapper.ParseLine(line)!);

        Assert.False(result.Success);
        Assert.Equal(BleCommandOutcome.CarAbsent, result.Outcome);
        Assert.Equal(BleCommandPhase.Scan, result.Phase);
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

    // The background scan writes to the same pipe as request answers. Its lines carry no id, so the only thing that
    // stops them being mistaken for a response is the routing below - this is the desynchronisation risk of the whole
    // stream design, pinned here.
    [Fact]
    public void ScanEventsAreNeverMistakenForAResponse()
    {
        const string digest = """
            {"kind":"adv","windowMs":500,"total":42,"devices":[{"addr":"90:2e:ab:23:19:4a","name":"S612fafca57f07c21C","rssi":-65,"count":12,"named":5,"connectable":true}]}
            """;
        const string state = """{"kind":"scan","state":"paused","reason":"radio handed over"}""";
        const string answer = """{"kind":"result","id":7,"ok":true,"outcome":"ok"}""";

        var parsedDigest = WorkerResponseMapper.ParseLine(digest);
        var parsedState = WorkerResponseMapper.ParseLine(state);
        var parsedAnswer = WorkerResponseMapper.ParseLine(answer);

        Assert.True(WorkerResponseMapper.IsScanEvent(parsedDigest));
        Assert.True(WorkerResponseMapper.IsScanEvent(parsedState));
        Assert.False(WorkerResponseMapper.IsScanEvent(parsedAnswer));

        //Whatever the request in flight is, a scan event is never its answer.
        foreach (var pendingId in new[] { 0, 1, 7, 4711 })
        {
            Assert.False(WorkerResponseMapper.IsResponseTo(parsedDigest, pendingId));
            Assert.False(WorkerResponseMapper.IsResponseTo(parsedState, pendingId));
        }
        Assert.True(WorkerResponseMapper.IsResponseTo(parsedAnswer, 7));
        //A stale answer to an older request must not satisfy a newer one either.
        Assert.False(WorkerResponseMapper.IsResponseTo(parsedAnswer, 8));
    }

    [Fact]
    public void ParsesAnAdvertisementDigest()
    {
        const string line = """
            {"kind":"adv","windowMs":500,"total":42,"truncated":false,
             "devices":[{"addr":"90:2e:ab:23:19:4a","name":"S612fafca57f07c21C","rssi":-65,"count":12,"named":5,"connectable":true},
                        {"addr":"11:11:11:11:11:11","rssi":-80,"count":3,"named":0,"connectable":false}]}
            """;
        var parsed = WorkerResponseMapper.ParseLine(line)!;

        Assert.Equal("adv", parsed.Kind);
        Assert.Equal(42, parsed.Total);
        Assert.Equal(500, parsed.WindowMs);
        Assert.NotNull(parsed.Devices);
        Assert.Equal(2, parsed.Devices!.Count);
        var car = parsed.Devices.Single(d => d.Addr == "90:2e:ab:23:19:4a");
        Assert.Equal("S612fafca57f07c21C", car.Name);
        Assert.Equal(12, car.Count);
        Assert.Equal(5, car.Named);
        Assert.Equal(-65, car.Rssi);
        Assert.Null(parsed.Devices.Single(d => d.Addr == "11:11:11:11:11:11").Name);
    }

    [Fact]
    public void ParsesAScanStateEvent()
    {
        var parsed = WorkerResponseMapper.ParseLine("""{"kind":"scan","state":"error","reason":"adapter gone"}""")!;
        Assert.Equal("scan", parsed.Kind);
        Assert.Equal("error", parsed.State);
        Assert.Equal("adapter gone", parsed.Reason);
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
