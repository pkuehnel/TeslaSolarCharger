using Newtonsoft.Json;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class SetupCacheService(
    ITscConfigurationService tscConfigurationService,
    IConstants constants)
    : ISetupCacheService
{
    public async Task<DtoSetupCache?> GetSetupCache()
    {
        var cacheJson = await tscConfigurationService.GetConfigurationValueByKey(constants.SetupCacheKey);
        if (string.IsNullOrEmpty(cacheJson))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<DtoSetupCache>(cacheJson);
    }

    public async Task UpdateSetupCache(DtoSetupCache setupCache)
    {
        var cacheJson = JsonConvert.SerializeObject(setupCache);
        await tscConfigurationService.SetConfigurationValueByKey(constants.SetupCacheKey, cacheJson);
    }

    public async Task DeleteSetupCache()
    {
        await tscConfigurationService.SetConfigurationValueByKey(constants.SetupCacheKey, string.Empty);
    }
}
