namespace TeslaSolarCharger.Server.Contracts;

public interface ICoreService
{
    Task<string?> GetCurrentVersion();
    void LogVersion();
}
