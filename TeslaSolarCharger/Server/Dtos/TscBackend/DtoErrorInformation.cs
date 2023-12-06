namespace TeslaSolarCharger.Server.Dtos.TscBackend;

public class DtoErrorInformation
{
    public string InstallationId { get; set; }
    public string Source { get; set; }
    public string MethodName { get; set; }
    public string Message { get; set; }
    public string Version { get; set; }
    public string? StackTrace { get; set; }
}
