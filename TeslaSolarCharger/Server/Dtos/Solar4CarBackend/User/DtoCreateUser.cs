﻿namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend.User;

public class DtoCreateUser(string userName, string password)
{
    public string UserName { get; set; } = userName;
    public string? Email { get; set; }
    public string Password { get; set; } = password;
}
