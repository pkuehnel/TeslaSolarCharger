using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Kostal;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Kostal;

public class KostalHybridInverterHomeBatteryModeService : IHomeBatteryModeSetupService
{
    private const int BatteryChargePowerSetpointAddress = 1034;
    private const int MaxBatteryDischargePowerLimitAddress = 1040;

    private readonly ILogger<KostalHybridInverterHomeBatteryModeService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public KostalHybridInverterHomeBatteryModeService(ILogger<KostalHybridInverterHomeBatteryModeService> logger,
        IServiceScopeFactory serviceScopeFactory, ITemplateValueConfigurationService templateValueConfigurationService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _templateValueConfigurationService = templateValueConfigurationService;
    }

    public async Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(GetControllersAsync));
        var templateValueGatherType = TemplateValueGatherType.KostalHybridInverterModbus;
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => c.GatherType == templateValueGatherType;
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var result = new List<DtoHomeBatteryModeController>();
        foreach (var config in configs)
        {
            if (config.Configuration == default)
            {
                continue;
            }
            var kostalConfig = config.Configuration.ToObject<DtoKostalModbusConfiguration>();
            if (kostalConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (!kostalConfig.EnableHomeBatteryControl)
            {
                continue;
            }
            if (string.IsNullOrEmpty(kostalConfig.Host))
            {
                _logger.LogError("Host for template configuration ID {id} is null or empty.", config.Id);
                continue;
            }
            var modbusConfig = new DtoModbusConfiguration()
            {
                Host = kostalConfig.Host,
                Port = kostalConfig.Port,
                UnitIdentifier = kostalConfig.UnitId,
                Endianess = ModbusEndianess.LittleEndian,
                ConnectDelayMilliseconds = 0,
                ReadTimeoutMilliseconds = 1000,
                Id = config.Id,
            };
            var maxChargePowerW = kostalConfig.MaxBatteryChargePowerW;
            var serviceScopeFactory = _serviceScopeFactory;
            result.Add(new DtoHomeBatteryModeController(config.Id, config.Name ?? string.Empty,
                async (mode, ct) =>
                {
                    using var executionScope = serviceScopeFactory.CreateScope();
                    var modbusValueExecutionService = executionScope.ServiceProvider
                        .GetRequiredService<IModbusValueExecutionService>();
                    foreach (var registerWrite in GetRegisterWrites(mode, maxChargePowerW))
                    {
                        ct.ThrowIfCancellationRequested();
                        await modbusValueExecutionService
                            .WriteValue(modbusConfig, ModbusValueType.Float, registerWrite.Address, registerWrite.Value,
                                ModbusWriteFunction.WriteMultipleRegisters, false)
                            .ConfigureAwait(false);
                    }
                },
                //External battery control setpoints time out on the inverter when not refreshed, which also acts as
                //failsafe in case TSC crashes.
                TimeSpan.FromSeconds(60)));
        }
        return result;
    }

    public static List<(int Address, decimal Value)> GetRegisterWrites(HomeBatteryMode mode, int maxChargePowerW)
    {
        return mode switch
        {
            //Resets a previously forced charge. A discharge limit of a previous hold mode times out on the inverter.
            HomeBatteryMode.Normal => new()
            {
                (BatteryChargePowerSetpointAddress, 0),
            },
            HomeBatteryMode.Hold => new()
            {
                (MaxBatteryDischargePowerLimitAddress, 0),
            },
            //Negative charge power setpoint forces charging.
            HomeBatteryMode.Charge => new()
            {
                (BatteryChargePowerSetpointAddress, -maxChargePowerW),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode can not be written to Kostal hybrid inverter"),
        };
    }
}
