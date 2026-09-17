using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericModbus;

/// <summary>
/// Value gathering for all vendors defined via fixed register maps in <see cref="ModbusTemplateDefinitions"/>.
/// </summary>
public class GenericModbusTemplateValueSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<GenericModbusTemplateValueSetupService> _logger;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public GenericModbusTemplateValueSetupService(ILogger<GenericModbusTemplateValueSetupService> logger, IConstants constants,
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
        var gatherTypes = ModbusTemplateDefinitions.Definitions.Keys.ToList();
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
            var typedConfig = config.Configuration.ToObject<DtoGenericModbusTemplateValueConfiguration>();
            if (typedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (string.IsNullOrEmpty(typedConfig.Host))
            {
                _logger.LogError("Host for template configuration ID {id} is null or empty.", config.Id);
                continue;
            }
            var definition = ModbusTemplateDefinitions.Definitions[config.GatherType.Value];
            var modbusConfiguration = CreateModbusConfiguration(config.Id, typedConfig, definition);
            try
            {
                var refreshable = new DelegateRefreshableValue<decimal>(
                    _serviceScopeFactory,
                    async ct =>
                    {
                        using var executionScope = _serviceScopeFactory.CreateScope();
                        var modbusValueExecutionService = executionScope.ServiceProvider
                            .GetRequiredService<IModbusValueExecutionService>();
                        var values = new Dictionary<ValueKey, decimal>();
                        var resultId = 0;
                        foreach (var register in definition.ValueRegisters)
                        {
                            ct.ThrowIfCancellationRequested();
                            resultId++;
                            var resultConfiguration = new DtoModbusValueResultConfiguration
                            {
                                Id = resultId,
                                RegisterType = register.RegisterType,
                                ValueType = register.ValueType,
                                Address = register.Address,
                                Length = register.Length,
                                UsedFor = register.UsedFor,
                                Operator = register.Operator,
                                CorrectionFactor = register.CorrectionFactor,
                            };
                            var valueKey = new ValueKey(register.UsedFor, null, resultId);
                            try
                            {
                                var registerModbusConfiguration = modbusConfiguration;
                                if (register.UnitIdOverride != default)
                                {
                                    registerModbusConfiguration = CreateModbusConfiguration(config.Id, typedConfig, definition);
                                    registerModbusConfiguration.UnitIdentifier = register.UnitIdOverride;
                                }
                                var byteArray = await modbusValueExecutionService
                                    .GetResult(registerModbusConfiguration, resultConfiguration, false)
                                    .ConfigureAwait(false);
                                decimal value;
                                if (register.NotAvailableValue != default || register.Offset != 0)
                                {
                                    var neutralConfiguration = new DtoModbusValueResultConfiguration
                                    {
                                        Id = resultId,
                                        ValueType = register.ValueType,
                                        Operator = ValueOperator.Plus,
                                        CorrectionFactor = 1,
                                    };
                                    var rawValue = await modbusValueExecutionService
                                        .GetValue(byteArray, neutralConfiguration).ConfigureAwait(false);
                                    if (rawValue == register.NotAvailableValue)
                                    {
                                        throw new InvalidDataException(
                                            $"Register {register.Address} returned its not available sentinel value");
                                    }
                                    value = (rawValue + register.Offset) * register.CorrectionFactor
                                            * (register.Operator == ValueOperator.Minus ? -1 : 1);
                                }
                                else
                                {
                                    value = await modbusValueExecutionService
                                        .GetValue(byteArray, resultConfiguration)
                                        .ConfigureAwait(false);
                                }
                                values.TryAdd(valueKey, 0m);
                                values[valueKey] += value;
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                var logger = executionScope.ServiceProvider.GetRequiredService<ILogger<GenericModbusTemplateValueSetupService>>();
                                logger.LogError(ex,
                                    "Error while refreshing modbus value for configuration {configurationId} register {address}",
                                    modbusConfiguration.Id, register.Address);
                                throw;
                            }
                        }
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
                _logger.LogError(ex,
                    "Error while creating refreshable for modbus configuration {configurationId} ({host}:{port})",
                    modbusConfiguration.Id, modbusConfiguration.Host, modbusConfiguration.Port);
            }
        }
        return result;
    }

    public static DtoModbusConfiguration CreateModbusConfiguration(int configurationId,
        DtoGenericModbusTemplateValueConfiguration typedConfig, ModbusTemplateDefinition definition)
    {
        return new DtoModbusConfiguration()
        {
            Host = typedConfig.Host,
            Port = typedConfig.Port,
            UnitIdentifier = typedConfig.UnitId,
            Endianess = definition.Endianess,
            ConnectDelayMilliseconds = definition.ConnectDelayMilliseconds,
            ReadTimeoutMilliseconds = definition.ReadTimeoutMilliseconds,
            Id = configurationId,
        };
    }
}
