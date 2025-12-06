using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Kostal;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Kostal;

public class KostalHybridInverterSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<KostalHybridInverterSetupService> _logger;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;


    public KostalHybridInverterSetupService(ILogger<KostalHybridInverterSetupService> logger, IConstants constants,
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
        var templateValueGatherType = TemplateValueGatherType.KostalHybridInverterModbus;
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
            var deserializedConfig = config.Configuration.ToObject<DtoKostalModbusConfiguration>();
            if (deserializedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}", config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (string.IsNullOrEmpty(deserializedConfig.Host))
            {
                _logger.LogError("Host for template configuration ID {id} is null or empty. Json is: {json}", config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }

            var modbusConfig = new DtoModbusConfiguration()
            {
                Host = deserializedConfig.Host,
                Port = deserializedConfig.Port,
                UnitIdentifier = deserializedConfig.UnitId,
                Endianess = ModbusEndianess.LittleEndian,
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
                                ValueType = ModbusValueType.Float,
                                Address = 260,
                                Length = 2,
                                UsedFor = ValueUsage.InverterPower,
                                Operator = ValueOperator.Plus,
                                CorrectionFactor = 1,
                            },
                            new()
                            {
                                Id = 2,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.Float,
                                Address = 270,
                                Length = 2,
                                UsedFor = ValueUsage.InverterPower,
                                Operator = ValueOperator.Plus,
                                CorrectionFactor = 1,
                            },
                            new()
                            {
                                Id = 3,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.Float,
                                Address = 252,
                                Length = 2,
                                UsedFor = ValueUsage.GridPower,
                                Operator = ValueOperator.Minus,
                                CorrectionFactor = 1,
                            },
                            new()
                            {
                                Id = 4,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.Short,
                                Address = 582,
                                Length = 1,
                                UsedFor = ValueUsage.HomeBatteryPower,
                                Operator = ValueOperator.Minus,
                                CorrectionFactor = 1,
                            },
                            new()
                            {
                                Id = 5,
                                RegisterType = ModbusRegisterType.HoldingRegister,
                                ValueType = ModbusValueType.UShort,
                                Address = 514,
                                Length = 1,
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
                                var logger = executionScope.ServiceProvider.GetRequiredService<ILogger<KostalHybridInverterSetupService>>();
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
