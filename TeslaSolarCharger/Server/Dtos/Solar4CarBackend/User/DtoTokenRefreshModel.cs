namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend.User;

public class DtoTokenRefreshModel(string accessToken, string refreshToken)
{
    public string AccessToken { get; set; } = accessToken;
    public string RefreshToken { get; set; } = refreshToken;
}
