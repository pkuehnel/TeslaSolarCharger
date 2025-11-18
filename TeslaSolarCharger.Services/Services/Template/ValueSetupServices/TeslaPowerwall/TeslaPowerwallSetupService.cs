using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Solax;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.TeslaPowerwall;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Services.Services.Template.ValueSetupServices.TeslaPowerwall;

public class TeslaPowerwallSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<TeslaPowerwallSetupService> _logger;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public TeslaPowerwallSetupService(ILogger<TeslaPowerwallSetupService> logger,
        ITemplateValueConfigurationService templateValueConfigurationService,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _templateValueConfigurationService = templateValueConfigurationService;
        _serviceScopeFactory = serviceScopeFactory;
    }


    public ConfigurationType ConfigurationType => ConfigurationType.TemplateValue;
    
    public async Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval, List<int> configurationIds)
    {
        _logger.LogTrace("{method}({defaultInterval})", nameof(GetDecimalRefreshableValuesAsync), defaultInterval);
        var templateValueGatherType = TemplateValueGatherType.SolaxApi;
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => c.GatherType == templateValueGatherType && (configurationIds.Count == 0 || configurationIds.Contains(c.Id));
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var result = new List<DelegateRefreshableValue<decimal>>();
        foreach (var config in configs)
        {
            if (config.Configuration == default)
            {
                _logger.LogError("Template configuration with ID {id} has empty configuration", config.Id);
                continue;
            }
            var typedConfig = config.Configuration.ToObject<DtoTeslaPowerwallTemplateValueConfiguration>();
            if (typedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}", config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            try
            {
                var refreshable = new DelegateRefreshableValue<decimal>(
                    _serviceScopeFactory,
                    async ct =>
                    {
                        using var executionScope = _serviceScopeFactory.CreateScope();
                        var teslaFleetApiService = executionScope.ServiceProvider.GetRequiredService<ITeslaFleetApiService>();
                        var solaxDto = await GetSolaxDto(executionScope, typedConfig, ct);
                        var values = GetPvValuesFromSolaxDto(solaxDto);
                        return new(values);
                    },
                    defaultInterval,
                    _constants.SolarHistoricValueCapacity,
                    new(config.Id, ConfigurationType.TemplateValue)
                );

                result.Add(refreshable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating refreshable for Solax configuration {id} ({host})", config.Id, typedConfig.Host);
            }
        }
    }
}
