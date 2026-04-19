namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISmartCarApiService
{
    Task RefreshTokensIfRequired();
}
