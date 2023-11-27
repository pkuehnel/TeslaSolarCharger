using Microsoft.Extensions.Primitives;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class TeslaToken
{
    public int Id { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string IdToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string State { get; set; }
    public string TokenType { get; set; }
}
