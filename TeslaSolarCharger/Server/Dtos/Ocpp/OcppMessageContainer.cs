using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Server.Dtos.Ocpp.RequestTypes;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

public enum MessageTypeId          // OCPP 1.6 JSON
{
    Call = 2,
    CallResult = 3,
    CallError = 4
}

public interface IOcppMessage
{
    string Action { get; }          // e.g. “BootNotification”
}

// -------------------- 2.  Envelopes ---------------------------
[JsonConverter(typeof(OcppFrameConverter))]
public abstract class OcppFrame
{
    public MessageTypeId MessageTypeId { get; set; }
    public string UniqueId { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class Call<TPayload> : OcppFrame where TPayload : IOcppMessage
{
    public TPayload Payload { get; }

    public Call(TPayload payload)
    {
        MessageTypeId = MessageTypeId.Call;
        Payload = payload;
    }

    public string Action => Payload.Action;
}

public sealed class CallResult<TPayload> : OcppFrame
{
    public string Action { get; }
    public TPayload Payload { get; }

    public CallResult(string uniqueId, string action, TPayload payload)
    {
        MessageTypeId = MessageTypeId.CallResult;
        UniqueId = uniqueId;
        Action = action;
        Payload = payload;
    }
}

public sealed class CallError : OcppFrame
{
    public string ErrorCode { get; }
    public string ErrorDescription { get; }
    public JToken? ErrorDetails { get; }

    public CallError(string uniqueId, string code, string desc, JToken? details = null)
    {
        MessageTypeId = MessageTypeId.CallError;
        UniqueId = uniqueId;
        ErrorCode = code;
        ErrorDescription = desc;
        ErrorDetails = details;
    }
}


public class OcppFrameConverter : JsonConverter
{
    // Map action‑names to concrete CLR types
    private static readonly IReadOnlyDictionary<string, Type> RequestTypes = new Dictionary<string, Type>
    {
        ["BootNotification"] = typeof(BootNotificationRequest),
        ["Heartbeat"] = typeof(HeartbeatRequest),
        // ➜ add more here
    };

    private static readonly IReadOnlyDictionary<string, Type> ResponseTypes = new Dictionary<string, Type>
    {
        ["BootNotification"] = typeof(BootNotificationResponse),
        ["Heartbeat"] = typeof(HeartbeatResponse),
        // ➜ add more here
    };

    public override bool CanConvert(Type objectType) => typeof(OcppFrame).IsAssignableFrom(objectType);

    public override object ReadJson(JsonReader r, Type t, object? existing, JsonSerializer s)
    {
        // Entire OCPP message is a 4‑element JSON array
        var arr = JArray.Load(r);
        if (arr.Count < 4) throw new JsonException("Invalid OCPP frame length.");

        var msgType = (MessageTypeId)arr[0].Value<int>();
        var uniqueId = arr[1].Value<string>() ?? "";
        var action = arr[2].Value<string>() ?? "";
        var payloadEl = arr[3];

        return msgType switch
        {
            MessageTypeId.Call => BuildCall(uniqueId, action, payloadEl, s),
            MessageTypeId.CallResult => BuildCallResult(uniqueId, action, payloadEl, s),
            MessageTypeId.CallError => BuildCallError(uniqueId, payloadEl),
            _ => throw new JsonException($"Unknown MessageTypeId {msgType}")
        };
    }

    private static object BuildCall(string uid, string action, JToken payload, JsonSerializer s)
    {
        if (!RequestTypes.TryGetValue(action, out var dtoType))
            throw new JsonException($"Unhandled action {action}");

        var dto = (IOcppMessage)payload.ToObject(dtoType, s)!;
        var frameType = typeof(Call<>).MakeGenericType(dtoType);
        return Activator.CreateInstance(frameType, dto)!;
    }

    private static object BuildCallResult(string uid, string action, JToken payload, JsonSerializer s)
    {
        if (!ResponseTypes.TryGetValue(action, out var dtoType))
            throw new JsonException($"Unhandled CALLRESULT for action {action}");

        var dto = payload.ToObject(dtoType, s)!;
        var frameType = typeof(CallResult<>).MakeGenericType(dtoType);
        return Activator.CreateInstance(frameType, uid, action, dto)!;
    }

    private static object BuildCallError(string uid, JToken payload)
    {
        if (payload is not JArray arr || arr.Count < 2)
            throw new JsonException("Invalid CALLERROR payload");

        var code = arr[0].Value<string>() ?? "";
        var desc = arr[1].Value<string>() ?? "";
        var details = arr.Count > 2 ? arr[2] : null;

        return new CallError(uid, code, desc, details);
    }

    public override void WriteJson(JsonWriter w, object? value, JsonSerializer s)
    {
        switch (value)
        {
            case Call<IOcppMessage> call:
                w.WriteStartArray();
                w.WriteValue((int)call.MessageTypeId);
                w.WriteValue(call.UniqueId);
                w.WriteValue(call.Action);
                s.Serialize(w, call.Payload);
                w.WriteEndArray();
                break;

            case CallResult<object> result:
                w.WriteStartArray();
                w.WriteValue((int)result.MessageTypeId);
                w.WriteValue(result.UniqueId);
                w.WriteValue(result.Action);
                s.Serialize(w, result.Payload);
                w.WriteEndArray();
                break;

            case CallError err:
                w.WriteStartArray();
                w.WriteValue((int)err.MessageTypeId);
                w.WriteValue(err.UniqueId);
                w.WriteValue("");                 // empty action field
                w.WriteStartArray();
                w.WriteValue(err.ErrorCode);
                w.WriteValue(err.ErrorDescription);
                s.Serialize(w, err.ErrorDetails);
                w.WriteEndArray();
                w.WriteEndArray();
                break;

            default:
                throw new JsonException($"Unsupported frame type {value?.GetType().Name}");
        }
    }
}
