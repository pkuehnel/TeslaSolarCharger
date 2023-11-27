namespace TeslaSolarCharger.Server.Dtos;

public class DtoTeslaToken
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string IdToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string TokenType { get; set; }
}
