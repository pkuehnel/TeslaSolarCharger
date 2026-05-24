using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISetupCacheService
{
    Task<DtoSetupCache?> GetSetupCache();
    Task UpdateSetupCache(DtoSetupCache setupCache);
    Task DeleteSetupCache();
}
