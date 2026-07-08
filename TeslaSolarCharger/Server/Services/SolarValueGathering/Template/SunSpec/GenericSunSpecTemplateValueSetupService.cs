using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
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
                    var modbusValueExecutionService = executionScope.ServiceProvider.GetRequiredService<IModbusValueExecutionService>();
                    foreach (var valueRead in definition.ValueReads)
                    {
                        ct.ThrowIfCancellationRequested();
                        resultId++;
                        var value = await ReadValueAsync(sunSpecClient, modbusValueExecutionService, modbusConfiguration, valueRead, ct).ConfigureAwait(false);
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

    private async Task<decimal?> ReadValueAsync(ISunSpecClient sunSpecClient, IModbusValueExecutionService modbusValueExecutionService,
        DtoModbusConfiguration modbusConfiguration, SunSpecValueRead valueRead, CancellationToken cancellationToken)
    {
        decimal sum = 0;
        var anyComponentRead = false;
        foreach (var component in valueRead.Components)
        {
            var componentValue = await ReadComponentAsync(sunSpecClient, modbusValueExecutionService, modbusConfiguration, component, cancellationToken)
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

    private async Task<decimal?> ReadComponentAsync(ISunSpecClient sunSpecClient, IModbusValueExecutionService modbusValueExecutionService,
        DtoModbusConfiguration modbusConfiguration, SunSpecValueComponent component, CancellationToken cancellationToken)
    {
        if (component.PlainRegisterAddress != default)
        {
            return await ReadPlainRegisterAsync(modbusValueExecutionService, modbusConfiguration, component, cancellationToken)
                .ConfigureAwait(false);
        }
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

    private static async Task<decimal?> ReadPlainRegisterAsync(IModbusValueExecutionService modbusValueExecutionService,
        DtoModbusConfiguration sunSpecConfiguration, SunSpecValueComponent component, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        //Plain vendor registers (e.g. SolarEdge battery registers) may use a different endianess than the SunSpec reads
        var plainConfiguration = new DtoModbusConfiguration
        {
            Host = sunSpecConfiguration.Host,
            Port = sunSpecConfiguration.Port,
            UnitIdentifier = sunSpecConfiguration.UnitIdentifier,
            Endianess = component.PlainRegisterEndianess,
            ConnectDelayMilliseconds = sunSpecConfiguration.ConnectDelayMilliseconds,
            ReadTimeoutMilliseconds = sunSpecConfiguration.ReadTimeoutMilliseconds,
            Id = sunSpecConfiguration.Id,
        };
        var length = component.PlainRegisterValueType is ModbusValueType.Int or ModbusValueType.UInt or ModbusValueType.Float ? 2 : 1;
        var resultConfiguration = new DtoModbusValueResultConfiguration
        {
            Id = 1,
            RegisterType = ModbusRegisterType.HoldingRegister,
            ValueType = component.PlainRegisterValueType,
            Address = component.PlainRegisterAddress!.Value,
            Length = length,
            Operator = ValueOperator.Plus,
            CorrectionFactor = 1,
        };
        var byteArray = await modbusValueExecutionService.GetResult(plainConfiguration, resultConfiguration, false).ConfigureAwait(false);
        //SolarEdge float32 registers return NaN when the value is not available (e.g. no battery installed)
        if (component.PlainRegisterValueType == ModbusValueType.Float)
        {
            var floatValue = BitConverter.ToSingle(byteArray, 0);
            if (float.IsNaN(floatValue) || float.IsInfinity(floatValue))
            {
                return default;
            }
            return (decimal)floatValue;
        }
        return await modbusValueExecutionService.GetValue(byteArray, resultConfiguration).ConfigureAwait(false);
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
