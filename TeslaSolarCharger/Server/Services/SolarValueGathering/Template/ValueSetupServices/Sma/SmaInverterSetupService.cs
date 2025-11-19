using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Sma;

public class SmaInverterSetupService : IRefreshableValueSetupService
{
    private readonly ILogger<SmaInverterSetupService> _logger;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public SmaInverterSetupService(ILogger<SmaInverterSetupService> logger, IConstants constants,
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
        var templateValueGatherType = TemplateValueGatherType.SmaInverterModbus;
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
                                Address = 30775,
                                Length = 2,
                                UsedFor = ValueUsage.InverterPower,
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

                                if (value < 0)
                                {
                                    var logger = executionScope.ServiceProvider.GetRequiredService<ILogger<SmaInverterSetupService>>();
                                    logger.LogDebug("Received raw value of {value} lower than 0. This is normal behaviour for SMA inverters in standby, using 0 for future calculations", value);
                                    value = 0;
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
                                var logger = executionScope.ServiceProvider.GetRequiredService<ILogger<SmaInverterSetupService>>();
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
