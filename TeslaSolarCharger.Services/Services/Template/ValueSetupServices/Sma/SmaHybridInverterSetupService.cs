using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Template.ValueSetupServices.Sma;

public class SmaHybridInverterSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<SmaHybridInverterSetupService> _logger;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;


    public SmaHybridInverterSetupService(ILogger<SmaHybridInverterSetupService> logger, IConstants constants,
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
        var templateValueGatherType = TemplateValueGatherType.SmaHybridInverterModbus;
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => c.GatherType == templateValueGatherType && (configurationIds.Count == 0 || configurationIds.Contains(c.Id));
        var smaInverterConfigs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);

        var result = new List<DelegateRefreshableValue<decimal>>();
        var modbusConfigurations = new List<DtoModbusConfiguration>();
        foreach (var config in smaInverterConfigs)
        {
            if (config.Configuration == default)
            {
                _logger.LogError("Template configuration with ID {id} has empty configuration", config.Id);
                continue;
            }
            var smaConfig = config.Configuration.ToObject<DtoSmaInverterTemplateValueConfiguration>();
            if (smaConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}", config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }

            var modbusConfig = new DtoModbusConfiguration()
            {
                Host = smaConfig.Host,
                Port = smaConfig.Port,
                UnitIdentifier = smaConfig.UnitId,
                Endianess = ModbusEndianess.BigEndian,
                ConnectDelayMilliseconds = 0,
                ReadTimeoutMilliseconds = 1000,
                Id = config.Id,
            };
            modbusConfigurations.Add(modbusConfig);
        }
        foreach (var modbusConfiguration in modbusConfigurations)
        {
            try
            {
                var configuration = modbusConfiguration;
                var refreshable = new DelegateRefreshableValue<decimal>(
                    _serviceScopeFactory,
                    async ct =>
                    {
                        using var executionScope = _serviceScopeFactory.CreateScope();
                        var modbusValueExecutionService = executionScope.ServiceProvider
                            .GetRequiredService<IModbusValueExecutionService>();

                        var resultConfigurations = new List<DtoModbusValueResultConfiguration>
                        {
                            new()
                            {
                                Id = 1,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.Int,
                                Address = 30773,
                                Length = 2,
                                UsedFor = ValueUsage.InverterPower,
                                Operator = ValueOperator.Plus,
                                CorrectionFactor = 1,
                            },
                            new()
                            {
                                Id = 2,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.Int,
                                Address = 30961,
                                Length = 2,
                                UsedFor = ValueUsage.InverterPower,
                                Operator = ValueOperator.Plus,
                                CorrectionFactor = 1,
                            },
                            new()
                            {
                                Id = 3,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.UInt,
                                Address = 31395,
                                Length = 2,
                                UsedFor = ValueUsage.HomeBatteryPower,
                                Operator = ValueOperator.Minus,
                                CorrectionFactor = 1,
                            },
                            new()
                            {
                                Id = 4,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.UInt,
                                Address = 31393,
                                Length = 2,
                                UsedFor = ValueUsage.HomeBatteryPower,
                                Operator = ValueOperator.Plus,
                                CorrectionFactor = 1,
                            },
                            new()
                            {
                                Id = 5,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.UInt,
                                Address = 30845,
                                Length = 2,
                                UsedFor = ValueUsage.HomeBatterySoc,
                                Operator = ValueOperator.Plus,
                                CorrectionFactor = 1,
                            },
                        };

                        var values = new Dictionary<ValueKey, decimal>();
                        foreach (var resultConfiguration in resultConfigurations)
                        {
                            ct.ThrowIfCancellationRequested();
                            var valueKey = new ValueKey(resultConfiguration.UsedFor, null, resultConfiguration.Id);
                            try
                            {
                                var byteArray = await modbusValueExecutionService
                                    .GetResult(configuration, resultConfiguration, false)
                                    .ConfigureAwait(false);
                                var value = await modbusValueExecutionService
                                    .GetValue(byteArray, resultConfiguration)
                                    .ConfigureAwait(false);

                                values.TryAdd(valueKey, 0m);
                                values[valueKey] += value;

                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                var logger = executionScope.ServiceProvider.GetRequiredService<ILogger<SmaHybridInverterSetupService>>();
                                logger.LogError(
                                    ex,
                                    "Error while refreshing modbus value for configuration {configurationId} result {resultId}",
                                    configuration.Id,
                                    resultConfiguration.Id);
                                throw;
                            }
                        }

                        return new(values);
                    },
                    defaultInterval,
                    _constants.SolarHistoricValueCapacity,
                    new(configuration.Id, ConfigurationType.TemplateValue)
                );

                result.Add(refreshable);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while creating refreshable for modbus configuration {configurationId} ({host}:{port})",
                    modbusConfiguration.Id,
                    modbusConfiguration.Host,
                    modbusConfiguration.Port);
            }
        }

        return result;
    }
}
