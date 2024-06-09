using System.Net;

namespace TeslaSolarCharger.Shared.Dtos.Ble;

public class DtoBleResult
{
    public HttpStatusCode StatusCode { get; set; }
    public string Message { get; set; }
    public bool Success { get; set; }
}
