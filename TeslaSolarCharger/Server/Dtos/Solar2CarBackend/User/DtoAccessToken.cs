namespace TeslaSolarCharger.Server.Dtos.Solar2CarBackend.User;

public class DtoAccessToken(string accessToken, string refreshToken)
{
    public string AccessToken { get; set; } = accessToken;
    public string RefreshToken { get; set; } = refreshToken;
    public long ExpiresAt { get; set; }
}
