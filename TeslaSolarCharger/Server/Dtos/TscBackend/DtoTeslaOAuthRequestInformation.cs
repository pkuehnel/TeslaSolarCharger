namespace TeslaSolarCharger.Server.Dtos.TscBackend;

public class DtoTeslaOAuthRequestInformation
{
    public string ClientId { get; set; }
    public string Prompt { get; set; }
    public string RedirectUri { get; set; }
    public string ResponseType { get; set; }
    public string Scope { get; set; }
    public string State { get; set; }
}
