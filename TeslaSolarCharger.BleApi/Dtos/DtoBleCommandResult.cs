using TeslaSolarCharger.BleApi.Enums;

namespace TeslaSolarCharger.BleApi.Dtos;

public class DtoBleCommandResult
{
    public string? ResultMessage { get; set; }
    public bool Success { get; set; }
    public ErrorType? ErrorType { get; set; }
    public string? CarErrorMessage { get; set; }

    /// <summary>
    /// Diagnostic log of tesla-control, only filled when the command was executed with debug enabled.
    /// </summary>
    public string? DebugOutput { get; set; }
}