namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend.User;

public class DtoLogin(string userName, string password)
{
    public string UserName { get; set; } = userName;
    public string Password { get; set; } = password;
}
