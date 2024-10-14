using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Ble;

public class DtoBleCommandResult
{
    public string? ResultMessage { get; set; }
    public bool Success { get; set; }
    public ErrorType? ErrorType { get; set; }
    public string? CarErrorMessage { get; set; }
}
