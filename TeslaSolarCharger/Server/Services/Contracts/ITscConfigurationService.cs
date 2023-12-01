namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITscConfigurationService
{
    Task<Guid> GetInstallationId();
}
