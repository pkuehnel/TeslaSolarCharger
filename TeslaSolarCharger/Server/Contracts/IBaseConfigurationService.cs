using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Contracts;

public interface IBaseConfigurationService
{
    Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration);
}