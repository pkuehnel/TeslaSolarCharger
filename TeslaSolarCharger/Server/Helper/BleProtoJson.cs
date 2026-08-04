using Google.Protobuf;

namespace TeslaSolarCharger.Server.Helper;

/// <summary>
/// Decodes the protojson the BLE container prints into Tesla's own generated protobuf types.
/// </summary>
/// <remarks>
/// The single place in TSC that builds a <see cref="JsonParser"/>, because it must be built with
/// <c>IgnoreUnknownFields</c>: the parser is strict by default and throws on any field it does not know, and Tesla
/// adds fields to these messages every few months. Without this, a car firmware update would break decoding for
/// everyone in the field rather than degrading gracefully.
/// </remarks>
public static class BleProtoJson
{
    private static readonly JsonParser Parser =
        new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    /// <summary>
    /// Parses a container answer, or returns null if it is empty or not a valid representation of
    /// <typeparamref name="T"/>. Never throws: a failed BLE read is an expected condition, not an error.
    /// </summary>
    public static T? TryParse<T>(string? json)
        where T : class, IMessage<T>, new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return Parser.Parse<T>(json);
        }
        //Malformed JSON (the container returns plain text on some failures). Listed separately because it is NOT a
        //subclass of InvalidProtocolBufferException, despite what the name suggests.
        catch (InvalidJsonException)
        {
            return null;
        }
        //Valid JSON that does not fit the message.
        catch (InvalidProtocolBufferException)
        {
            return null;
        }
    }
}
