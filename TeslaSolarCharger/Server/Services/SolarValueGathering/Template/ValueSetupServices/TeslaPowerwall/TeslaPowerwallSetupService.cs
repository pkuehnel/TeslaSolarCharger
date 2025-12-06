using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.TeslaPowerwall;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.TeslaPowerwall;

public class TeslaPowerwallSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<TeslaPowerwallSetupService> _logger;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConstants _constants;

    public TeslaPowerwallSetupService(ILogger<TeslaPowerwallSetupService> logger,
        ITemplateValueConfigurationService templateValueConfigurationService,
        IServiceScopeFactory serviceScopeFactory, IConstants constants)
    {
        _logger = logger;
        _templateValueConfigurationService = templateValueConfigurationService;
        _serviceScopeFactory = serviceScopeFactory;
        _constants = constants;
    }


    public ConfigurationType ConfigurationType => ConfigurationType.TemplateValue;

    public async Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval, List<int> configurationIds)
    {
        _logger.LogTrace("{method}({defaultInterval})", nameof(GetDecimalRefreshableValuesAsync), defaultInterval);
        var templateValueGatherType = TemplateValueGatherType.TeslaPowerwallFleetApi;
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => c.GatherType == templateValueGatherType && (configurationIds.Count == 0 || configurationIds.Contains(c.Id));
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var result = new List<DelegateRefreshableValue<decimal>>();
        //Reduce Tesla API calls
        var minInterval = TimeSpan.FromSeconds(10);
        if (defaultInterval < minInterval)
        {
            defaultInterval = minInterval;
        }
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
                    async _ =>
                    {
                        using var executionScope = _serviceScopeFactory.CreateScope();
                        var teslaFleetApiService = executionScope.ServiceProvider.GetRequiredService<ITeslaFleetApiService>();
                        if (typedConfig.EnergySiteId == default)
                        {
                            throw new InvalidDataException("Energy site can not be null");
                        }
                        var teslaResponse = await teslaFleetApiService.GetEnergyLiveStatus(typedConfig.EnergySiteId.Value.ToString());
                        if (!teslaResponse.StatusCode.IsSuccessStatusCode())
                        {
                            throw new InvalidOperationException("Getting energy live status form Tesla did not result in success status code");
                        }
                        var json = teslaResponse.JsonResponse;
                        if (string.IsNullOrEmpty(json))
                        {
                            throw new InvalidDataException("Json string of energy live status is empty");
                        }
                        var restValueExecutionService = executionScope.ServiceProvider.GetRequiredService<IRestValueExecutionService>();
                        var id = 1;
                        var values = new ConcurrentDictionary<ValueKey, decimal>();

                        // 1. Inverter Configuration
                        var inverterConfiguration = new DtoJsonXmlResultConfiguration()
                        {
                            CorrectionFactor = 1m,
                            Operator = ValueOperator.Plus,
                            NodePattern = "$.response.solar_power",
                        };
                        values.TryAdd(new ValueKey(ValueUsage.InverterPower, null, id++),
                            restValueExecutionService.GetValue(json, NodePatternType.Json, inverterConfiguration));

                        // 2. Grid Configuration
                        var gridConfiguration = new DtoJsonXmlResultConfiguration()
                        {
                            CorrectionFactor = 1m,
                            Operator = ValueOperator.Minus,
                            NodePattern = "$.response.grid_power",
                        };
                        values.TryAdd(new ValueKey(ValueUsage.GridPower, null, id++),
                            restValueExecutionService.GetValue(json, NodePatternType.Json, gridConfiguration));

                        // 3. Battery Power Configuration
                        var batteryPowerConfiguration = new DtoJsonXmlResultConfiguration()
                        {
                            CorrectionFactor = 1m,
                            Operator = ValueOperator.Plus,
                            NodePattern = "$.response.battery_power",
                        };
                        values.TryAdd(new ValueKey(ValueUsage.HomeBatteryPower, null, id++),
                            restValueExecutionService.GetValue(json, NodePatternType.Json, batteryPowerConfiguration));

                        // 4. Battery SoC Configuration
                        var batterySocConfiguration = new DtoJsonXmlResultConfiguration()
                        {
                            CorrectionFactor = 1m,
                            Operator = ValueOperator.Plus,
                            NodePattern = "$.response.percentage_charged",
                        };
                        values.TryAdd(new ValueKey(ValueUsage.HomeBatterySoc, null, id),
                            restValueExecutionService.GetValue(json, NodePatternType.Json, batterySocConfiguration));
                        return values;
                    },
                    defaultInterval,
                    _constants.SolarHistoricValueCapacity,
                    new(config.Id, ConfigurationType.TemplateValue)
                );
                result.Add(refreshable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating refreshable for Powerwall configuration configuration {id} ({energySiteId})", config.Id, typedConfig.EnergySiteId);
            }
        }
        return result;
    }
}
