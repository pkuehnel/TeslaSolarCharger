using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec;

/// <summary>
/// Value gathering for all vendors defined via SunSpec maps in <see cref="SunSpecTemplateDefinitions"/>.
/// </summary>
public class GenericSunSpecTemplateValueSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<GenericSunSpecTemplateValueSetupService> _logger;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public GenericSunSpecTemplateValueSetupService(ILogger<GenericSunSpecTemplateValueSetupService> logger, IConstants constants,
        IServiceScopeFactory serviceScopeFactory, ITemplateValueConfigurationService templateValueConfigurationService)
    {
        _logger = logger;
        _constants = constants;
        _serviceScopeFactory = serviceScopeFactory;
        _templateValueConfigurationService = templateValueConfigurationService;
    }

    public ConfigurationType ConfigurationType => ConfigurationType.TemplateValue;

    public async Task<List<DelegateRefreshableValue<decimal>>> GetDecimalRefreshableValuesAsync(TimeSpan defaultInterval,
        List<int> configurationIds)
    {
        _logger.LogTrace("{method}({defaultInterval})", nameof(GetDecimalRefreshableValuesAsync), defaultInterval);
        var gatherTypes = SunSpecTemplateDefinitions.Definitions.Keys.ToList();
        Expression<Func<TemplateValueConfiguration, bool>> expression = c =>
            gatherTypes.Contains(c.GatherType) && (configurationIds.Count == 0 || configurationIds.Contains(c.Id));
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var result = new List<DelegateRefreshableValue<decimal>>();
        foreach (var config in configs)
        {
            if (config.Configuration == default || config.GatherType == default)
            {
                _logger.LogError("Template configuration with ID {id} has empty configuration", config.Id);
                continue;
            }
            var typedConfig = config.Configuration.ToObject<DtoSunSpecTemplateValueConfiguration>();
            if (typedConfig == default || string.IsNullOrEmpty(typedConfig.Host))
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            var definition = SunSpecTemplateDefinitions.Definitions[config.GatherType.Value];
            var modbusConfiguration = CreateModbusConfiguration(config.Id, typedConfig);
            var refreshable = new DelegateRefreshableValue<decimal>(
                _serviceScopeFactory,
                async ct =>
                {
                    using var executionScope = _serviceScopeFactory.CreateScope();
                    var sunSpecClient = executionScope.ServiceProvider.GetRequiredService<ISunSpecClient>();
                    var values = new Dictionary<ValueKey, decimal>();
                    var resultId = 0;
                    foreach (var valueRead in definition.ValueReads)
                    {
                        ct.ThrowIfCancellationRequested();
                        resultId++;
                        var value = await ReadValueAsync(sunSpecClient, modbusConfiguration, valueRead, ct).ConfigureAwait(false);
                        if (value == default)
                        {
                            continue;
                        }
                        values.Add(new ValueKey(valueRead.UsedFor, null, resultId), value.Value);
                    }
                    return new(values);
                },
                defaultInterval,
                _constants.SolarHistoricValueCapacity,
                new(config.Id, ConfigurationType.TemplateValue)
            );
            result.Add(refreshable);
        }
        return result;
    }

    private async Task<decimal?> ReadValueAsync(ISunSpecClient sunSpecClient, DtoModbusConfiguration modbusConfiguration,
        SunSpecValueRead valueRead, CancellationToken cancellationToken)
    {
        decimal sum = 0;
        var anyComponentRead = false;
        foreach (var component in valueRead.Components)
        {
            var componentValue = await ReadComponentAsync(sunSpecClient, modbusConfiguration, component, cancellationToken)
                .ConfigureAwait(false);
            if (componentValue == default)
            {
                if (component.OptionalIfMissing)
                {
                    continue;
                }
                //A required component that is missing means the value can not be produced
                return default;
            }
            anyComponentRead = true;
            sum += componentValue.Value * (component.Operator == ValueOperator.Minus ? -1 : 1);
        }
        return anyComponentRead ? sum : default(decimal?);
    }

    private async Task<decimal?> ReadComponentAsync(ISunSpecClient sunSpecClient, DtoModbusConfiguration modbusConfiguration,
        SunSpecValueComponent component, CancellationToken cancellationToken)
    {
        foreach (var pointReference in component.PointFallbacks)
        {
            var value = await sunSpecClient.ReadValueAsync(modbusConfiguration, pointReference, cancellationToken)
                .ConfigureAwait(false);
            if (value != default)
            {
                return value;
            }
        }
        return default;
    }

    public static DtoModbusConfiguration CreateModbusConfiguration(int configurationId, DtoSunSpecTemplateValueConfiguration typedConfig)
    {
        return new DtoModbusConfiguration()
        {
            Host = typedConfig.Host,
            Port = typedConfig.Port,
            UnitIdentifier = typedConfig.UnitId,
            //SunSpec is always big endian on the wire
            Endianess = ModbusEndianess.BigEndian,
            ConnectDelayMilliseconds = 0,
            ReadTimeoutMilliseconds = 10000,
            Id = configurationId,
        };
    }
}
