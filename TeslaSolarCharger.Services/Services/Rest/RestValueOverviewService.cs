using Microsoft.Extensions.Logging;
using System.Linq;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Services.Services.Rest;

public class RestValueOverviewService(
    ILogger<RestValueOverviewService> logger,
    IRestValueConfigurationService restValueConfigurationService,
    IGenericValueService genericValueService) : IRestValueOverviewService
{
    public async Task<List<DtoValueConfigurationOverview>> GetRestValueOverviews()
    {
        logger.LogTrace("{method}()", nameof(GetRestValueOverviews));
        var restValueConfigurations = await restValueConfigurationService
            .GetFullRestValueConfigurationsByPredicate(c => true)
            .ConfigureAwait(false);
        var values = genericValueService.GetAllByPredicate(v =>
            v.SourceValueKey.ConfigurationType == ConfigurationType.RestSolarValue);
        var results = new List<DtoValueConfigurationOverview>();
        foreach (var dtoFullRestValueConfiguration in restValueConfigurations)
        {
            var resultConfigurations = await restValueConfigurationService
                .GetRestResultConfigurationByPredicate(c =>
                    c.RestValueConfigurationId == dtoFullRestValueConfiguration.Id)
                .ConfigureAwait(false);
            var overviewElement = new DtoValueConfigurationOverview(dtoFullRestValueConfiguration.Url)
            {
                Id = dtoFullRestValueConfiguration.Id,
            };
            results.Add(overviewElement);
            foreach (var resultConfiguration in resultConfigurations)
            {
                var dtoRestValueResult = new DtoOverviewValueResult
                {
                    Id = resultConfiguration.Id,
                    UsedFor = resultConfiguration.UsedFor,
                };
                var genericValues = values
                    .Where(v => v.SourceValueKey == new SourceValueKey(
                        dtoFullRestValueConfiguration.Id,
                        ConfigurationType.RestSolarValue))
                    .ToList();
                var calculatedValue = 0m;
                DateTimeOffset? lastUpdated = default;
                foreach (var genericValue in genericValues)
                {
                    foreach (var genericValueHistoricValue in genericValue.HistoricValues)
                    {
                        if (genericValueHistoricValue.Key.ResultConfigurationId == resultConfiguration.Id)
                        {
                            calculatedValue += genericValueHistoricValue.Value.Value;
                            var timestamp = genericValueHistoricValue.Value.Timestamp;
                            if (lastUpdated == default || lastUpdated < timestamp)
                            {
                                lastUpdated = timestamp;
                            }
                        }
                    }
                }

                dtoRestValueResult.CalculatedValue = calculatedValue;
                dtoRestValueResult.LastRefreshed = lastUpdated ?? default;
                overviewElement.Results.Add(dtoRestValueResult);
            }
        }

        return results;
    }
}
