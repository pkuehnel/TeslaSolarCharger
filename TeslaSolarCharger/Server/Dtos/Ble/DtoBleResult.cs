using System.Net;

namespace TeslaSolarCharger.Server.Dtos.Ble;

public class DtoBleResult
{
    public HttpStatusCode StatusCode { get; set; }
    public string Message { get; set; }
    public bool Success { get; set; }
}
