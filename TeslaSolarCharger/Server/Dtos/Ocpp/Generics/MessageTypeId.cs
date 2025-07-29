namespace TeslaSolarCharger.Server.Dtos.Ocpp.Generics;

/// <summary>
/// Values defined by the OCPP 1.6 specification (table 11).
/// </summary>
public enum MessageTypeId
{
    Call = 2,
    CallResult = 3,
    CallError = 4,
}
