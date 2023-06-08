using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Contracts;

public interface IBaseConfigurationService
{
    Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration);
    Task UpdateMaxCombinedCurrent(int? maxCombinedCurrent);
    void UpdatePowerBuffer(int powerBuffer);
}
