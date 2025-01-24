namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IPasswordGenerationService
{
    string GeneratePassword(int length);
}
