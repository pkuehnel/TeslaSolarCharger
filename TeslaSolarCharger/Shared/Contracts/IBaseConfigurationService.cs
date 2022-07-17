using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Shared.Contracts;

public interface IBaseConfigurationService
{
    Task<DtoBaseConfiguration> GetBaseConfiguration();
    Task SaveBaseConfiguration(DtoBaseConfiguration baseConfiguration);
    Task<bool> IsBaseConfigurationJsonRelevant();
}