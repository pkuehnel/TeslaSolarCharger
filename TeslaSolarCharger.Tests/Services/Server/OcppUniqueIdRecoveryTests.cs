using Xunit;
//The test class for this service shares its name, and in this namespace that name wins over the service itself.
using OcppService = TeslaSolarCharger.Server.Services.OcppWebSocketConnectionHandlingService;

namespace TeslaSolarCharger.Tests.Services.Server;

/// <summary>
/// Reading the head of a frame that could not be parsed is what lets TSC answer it, and answering is what keeps the
/// connection alive: a charge point that gets no answer to its Call blocks until its own message timeout and then
/// drops the connection without a close handshake. The action decides which answer it gets.
/// </summary>
public class OcppUniqueIdRecoveryTests
{
    /// <summary>
    /// The frame that costs the connection every two minutes in the field, shortened but shaped exactly like it: a
    /// charge point replaying a stored MeterValues whose payload is padded with newlines, truncated mid array and
    /// closed with a trailing comma. The head is intact, which is the whole reason it can be answered at all.
    /// </summary>
    private const string TruncatedMeterValues =
        "[2,\"edfe3e6b-6bec-4139-bf21-2b3bdb84711f\",\"MeterValues\",\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n"
        + "{\n    \"connectorId\": 1,\n    \"transactionId\": 1071,\n    \"meterValue\": [\n        {\n"
        + "            \"timestamp\": \"2026-08-05T16:52:00Z\",\n            \"sampledValue\": [\n"
        + "                {\n                    \"measurand\": \"Voltage\",\n                    \"value\": \"233\"\n"
        + "                },\n]}]}]";

    [Fact]
    public void RecoversTheHeadOfTheTruncatedFrameSeenInTheField()
    {
        var head = OcppService.TryRecoverHead(TruncatedMeterValues);
        Assert.Equal("edfe3e6b-6bec-4139-bf21-2b3bdb84711f", head.UniqueId);
        //Without the action the frame could only be answered with a CallError, which this charge point ignores.
        Assert.Equal("MeterValues", head.Action);
    }

    [Theory]
    [InlineData("[2,\"abc\",\"Heartbeat\",{}]", "abc", "Heartbeat")]
    [InlineData("[2,\"def\",\"StatusNotification\",{\"connectorId\":1}]", "def", "StatusNotification")]
    //Whitespace around the head is legal JSON and must not defeat the recovery.
    [InlineData(" [ 2 , \"spaced\" , \"Heartbeat\" , {} ]", "spaced", "Heartbeat")]
    //A CallResult has no action, and there is nothing to answer anyway.
    [InlineData("[3,\"cc15bbff121b4036971cb702dff97804\",{\"status\":\"Accepted\"}]", "cc15bbff121b4036971cb702dff97804", null)]
    //Truncated right after the id: still enough to send a CallError.
    [InlineData("[2,\"only-the-head\"", "only-the-head", null)]
    public void RecoversTheIdAndActionFromTheFrameHead(string raw, string expectedId, string? expectedAction)
    {
        var head = OcppService.TryRecoverHead(raw);
        Assert.Equal(expectedId, head.UniqueId);
        Assert.Equal(expectedAction, head.Action);
    }

    /// <summary>
    /// Answering with an id the charge point never sent is no better than not answering, so anything that is not
    /// recognisably a frame head yields nothing and the error goes out with an empty id instead.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[2]")]
    [InlineData("[2,")]
    [InlineData("[2,\"\",\"Heartbeat\",{}]")]
    public void ReportsNoIdWhenTheHeadIsUnusable(string raw)
    {
        Assert.Null(OcppService.TryRecoverHead(raw).UniqueId);
    }

    /// <summary>
    /// A broken head must not make some string out of the payload look like an id, so the search never leaves the
    /// head of the frame.
    /// </summary>
    [Fact]
    public void DoesNotTakeAnIdOutOfThePayloadWhenTheHeadIsBroken()
    {
        var raw = "[2" + new string(' ', 200) + ",\"deep-in-the-payload\",\"MeterValues\",{}]";
        Assert.Null(OcppService.TryRecoverHead(raw).UniqueId);
    }
}
