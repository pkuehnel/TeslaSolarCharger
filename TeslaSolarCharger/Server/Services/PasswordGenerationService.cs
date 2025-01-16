using System.Security.Cryptography;
using System.Text;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class PasswordGenerationService : IPasswordGenerationService
{
    private const string CharPool = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+[]{};:'\",.\\/?`~";


    public string GeneratePassword(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        var password = new StringBuilder();

        for (var i = 0; i < length; i++)
        {
            var randomNumber = new byte[1];
            rng.GetBytes(randomNumber);
            var charIndex = randomNumber[0] % CharPool.Length;
            password.Append(CharPool[charIndex]);
        }
        return password.ToString();
    }
}
