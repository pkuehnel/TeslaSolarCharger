using System.ComponentModel;
using System.Reflection;

namespace TeslaSolarCharger.Server.Dtos.Ocpp.Generics;

public enum CallErrorCode
{
    [Description("Requested Action is not known by receiver.")]
    NotImplemented,

    [Description("Requested Action is recognized but not supported by the receiver.")]
    NotSupported,

    [Description("An internal error occurred and the receiver was not able to process the requested Action successfully.")]
    InternalError,

    [Description("Payload for Action is incomplete.")]
    ProtocolError,

    [Description("During the processing of Action a security issue occurred preventing receiver from completing the Action successfully.")]
    SecurityError,

    [Description("Payload for Action is syntactically incorrect or not conform the PDU structure for Action.")]
    FormationViolation,

    [Description("Payload is syntactically correct but at least one field contains an invalid value.")]
    PropertyConstraintViolation,

    [Description("Payload for Action is syntactically correct but at least one of the fields violates occurrence constraints.")]
    OccurrenceConstraintViolation,

    [Description("Payload for Action is syntactically correct but at least one of the fields violates data type constraints.")]
    TypeConstraintViolation,

    [Description("Any other error not covered by the previous ones.")]
    GenericError,
}

public static class CallErrorCodeExtensions
{
    public static string GetDefaultDescription(this CallErrorCode code)
    {
        var member = typeof(CallErrorCode)
            .GetMember(code.ToString(), BindingFlags.Public | BindingFlags.Static);
        var attr = member[0]
            .GetCustomAttribute<DescriptionAttribute>(false);
        return attr?.Description ?? code.ToString();
    }
}

public class OcppCallErrorException : Exception
{
    /// <summary>
    /// The parsed, strongly-typed enum.
    /// </summary>
    public CallErrorCode Code { get; }

    /// <summary>
    /// The human-readable description (defaulted from the enum if none supplied).
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The exact raw string you got in the JSON payload.
    /// </summary>
    public string RawCode { get; }

    /// <summary>
    /// Construct from a known enum (you get its default description automatically).
    /// </summary>
    public OcppCallErrorException(CallErrorCode code, string? description = null)
        : base(description ?? code.GetDefaultDescription())
    {
        Code = code;
        Description = description ?? code.GetDefaultDescription();
        RawCode = code.ToString();
    }

    /// <summary>
    /// Construct from the raw JSON strings; we parse into the enum (fallback=GenericError),
    /// but we keep the original string around for reporting.
    /// </summary>
    public OcppCallErrorException(string rawCode, string? rawDescription = null)
        : this(ParseCode(rawCode),
               string.IsNullOrWhiteSpace(rawDescription)
                 ? ParseCode(rawCode).GetDefaultDescription()
                 : rawDescription)
    {
        RawCode = rawCode;
    }

    private static CallErrorCode ParseCode(string rawCode) =>
        Enum.TryParse<CallErrorCode>(rawCode, ignoreCase: true, out var parsed)
            ? parsed
            : CallErrorCode.GenericError;

    /// <summary>
    /// Now ex.ToString() == RawCode, so you always get precisely the
    /// protocol’s error‐code text back.
    /// </summary>
    public override string ToString() => RawCode;
}
