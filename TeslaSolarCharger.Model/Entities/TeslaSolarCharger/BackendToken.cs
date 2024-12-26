namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class BackendToken(string accessToken, string refreshToken)
{
    public int Id { get; set; }
    public string AccessToken { get; set; } = accessToken;
    public string RefreshToken { get; set; } = refreshToken;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
