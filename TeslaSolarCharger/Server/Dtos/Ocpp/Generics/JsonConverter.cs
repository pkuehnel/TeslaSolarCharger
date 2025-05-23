using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp.Generics;

/// <summary>
/// Registers with <c>JsonSerializerOptions.Converters</c> and makes every class that
/// derives from <see cref="OcppMessage"/> emit a JSON ARRAY instead of an object.
/// Reading is not implemented because the backend already parses the incoming
/// frames with <see cref="JsonDocument"/>.  If you need full round‑tripping later,
/// add a Read‑implementation in the nested converter.
/// </summary>
public sealed class OcppArrayConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(OcppMessage).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions opts)
    {
        var convType = typeof(OcppArrayMessageConverter<>).MakeGenericType(type);
        return (JsonConverter)Activator.CreateInstance(convType, opts)!;
    }

    // ---------------------------------------------------------------------
    //  The type‑specific converter created above
    // ---------------------------------------------------------------------
    private sealed class OcppArrayMessageConverter<T> : JsonConverter<T>
        where T : OcppMessage
    {
        private readonly JsonSerializerOptions _opts;

        // cache reflection look‑ups once
        private static readonly PropertyInfo? _actionProp =
            typeof(T).GetProperty("Action");

        private static readonly PropertyInfo? _payloadProp =
            typeof(T).GetProperty("Payload");

        private static readonly PropertyInfo? _errorCodeProp =
            typeof(T).GetProperty("ErrorCode");

        private static readonly PropertyInfo? _errorDescrProp =
            typeof(T).GetProperty("ErrorDescription");

        private static readonly PropertyInfo? _errorDetailsProp =
            typeof(T).GetProperty("ErrorDetails");

        public OcppArrayMessageConverter(JsonSerializerOptions opts) => _opts = opts;

        // --------------------------------------------------------------
        //  We only need Write – Read is done manually elsewhere
        // --------------------------------------------------------------
        public override void Write(Utf8JsonWriter w, T value, JsonSerializerOptions _)
        {
            w.WriteStartArray();

            switch (value.MessageTypeId)
            {
                case MessageTypeId.Call:
                    // [2, UniqueId, Action, Payload]
                    w.WriteNumberValue((int)MessageTypeId.Call);
                    w.WriteStringValue(value.UniqueId);
                    w.WriteStringValue((string)_actionProp!.GetValue(value)!);

                    var pl = _payloadProp!.GetValue(value);
                    JsonSerializer.Serialize(w, pl, pl?.GetType() ?? typeof(object), _opts);
                    break;

                case MessageTypeId.CallResult:
                    // [3, UniqueId, Payload]
                    w.WriteNumberValue((int)MessageTypeId.CallResult);
                    w.WriteStringValue(value.UniqueId);

                    var pr = _payloadProp!.GetValue(value);
                    JsonSerializer.Serialize(w, pr, pr?.GetType() ?? typeof(object), _opts);
                    break;

                case MessageTypeId.CallError:
                    // [4, UniqueId, errorCode, errorDescription, errorDetails]
                    w.WriteNumberValue((int)MessageTypeId.CallError);
                    w.WriteStringValue(value.UniqueId);
                    w.WriteStringValue((string)_errorCodeProp!.GetValue(value)!);
                    w.WriteStringValue((string)_errorDescrProp!.GetValue(value)!);

                    var ed = _errorDetailsProp!.GetValue(value);
                    JsonSerializer.Serialize(w, ed, ed?.GetType() ?? typeof(object), _opts);
                    break;

                default:
                    throw new JsonException($"Unsupported MessageTypeId: {value.MessageTypeId}");
            }

            w.WriteEndArray();
        }

        // --------------------------------------------------------------
        //  Deserialization not required for the current server flow
        // --------------------------------------------------------------
        public override T? Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions __)
            => throw new NotImplementedException(
                "Server parses incoming frames manually; deserialization is not required here.");
    }
}

