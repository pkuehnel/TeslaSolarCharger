using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Contracts;

public interface IBaseConfigurationService
{
    Task<DtoBaseConfiguration> GetBaseConfiguration();
    Task SaveBaseConfiguration(DtoBaseConfiguration baseConfiguration);
}