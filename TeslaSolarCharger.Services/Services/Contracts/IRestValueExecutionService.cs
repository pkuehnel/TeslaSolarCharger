using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Contracts;

public interface IRestValueExecutionService
{

    /// <summary>
    /// Get result for each configuration ID
    /// </summary>
    /// <param name="config">Rest Value configuration</param>
    /// <returns>Dictionary with with resultConfiguration as key and resulting value as Value</returns>
    /// <exception cref="InvalidOperationException">Throw if request results in not success status code</exception>
    Task<string> GetResult(DtoFullRestValueConfiguration config);
    decimal GetValue(string responseString, NodePatternType configNodePatternType, DtoRestValueResultConfiguration resultConfig);
    Task<string> DebugRestValueConfiguration(DtoFullRestValueConfiguration config);
    Task<List<DtoValueConfigurationOverview>> GetRestValueOverviews();
}
