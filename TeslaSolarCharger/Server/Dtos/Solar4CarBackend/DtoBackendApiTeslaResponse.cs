namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend;

public class DtoBackendApiTeslaResponse
{
    public int StatusCode { get; set; }
    public string? JsonResponse { get; set; }
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
}
