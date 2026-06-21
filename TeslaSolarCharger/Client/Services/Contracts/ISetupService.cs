using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface ISetupService
{
    Task<DtoSetupCache?> GetSetupCache();
    Task UpdateSetupCache(DtoSetupCache setupCache);
    Task DeleteSetupCache();
}
