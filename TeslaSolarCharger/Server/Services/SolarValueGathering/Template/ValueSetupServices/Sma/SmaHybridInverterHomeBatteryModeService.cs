using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Sma;

public class SmaHybridInverterHomeBatteryModeService : IHomeBatteryModeSetupService
{
    //Battery management operation modes of register 40236 (CmpBMS.OpMod)
    private const decimal OperatingModeDefault = 2424;
    private const decimal OperatingModeBatteryCharge = 2289;

    private const int OperatingModeAddress = 40236;
    private const int MinBatteryChargePowerAddress = 40793;
    private const int MaxBatteryChargePowerAddress = 40795;
    private const int MinBatteryDischargePowerAddress = 40797;
    private const int MaxBatteryDischargePowerAddress = 40799;
    private const int GridPowerSetpointAddress = 40801;

    private readonly ILogger<SmaHybridInverterHomeBatteryModeService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public SmaHybridInverterHomeBatteryModeService(ILogger<SmaHybridInverterHomeBatteryModeService> logger,
        IServiceScopeFactory serviceScopeFactory, ITemplateValueConfigurationService templateValueConfigurationService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _templateValueConfigurationService = templateValueConfigurationService;
    }

    public async Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(GetControllersAsync));
        var templateValueGatherType = TemplateValueGatherType.SmaHybridInverterModbus;
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
            var smaConfig = config.Configuration.ToObject<DtoSmaInverterTemplateValueConfiguration>();
            if (smaConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (!smaConfig.EnableHomeBatteryControl)
            {
                continue;
            }
            if (string.IsNullOrEmpty(smaConfig.Host))
            {
                _logger.LogError("Host for template configuration ID {id} is null or empty.", config.Id);
                continue;
            }
            var modbusConfig = new DtoModbusConfiguration()
            {
                Host = smaConfig.Host,
                Port = smaConfig.Port,
                UnitIdentifier = smaConfig.UnitId,
                Endianess = ModbusEndianess.BigEndian,
                ConnectDelayMilliseconds = 0,
                ReadTimeoutMilliseconds = 10000,
                Id = config.Id,
            };
            var maxChargePowerW = smaConfig.MaxBatteryChargePowerW;
            var maxDischargePowerW = smaConfig.MaxBatteryDischargePowerW;
            var serviceScopeFactory = _serviceScopeFactory;
            result.Add(new DtoHomeBatteryModeController(config.Id, config.Name ?? string.Empty,
                async (mode, ct) =>
                {
                    using var executionScope = serviceScopeFactory.CreateScope();
                    var modbusValueExecutionService = executionScope.ServiceProvider
                        .GetRequiredService<IModbusValueExecutionService>();
                    foreach (var registerWrite in GetRegisterWrites(mode, maxChargePowerW, maxDischargePowerW))
                    {
                        ct.ThrowIfCancellationRequested();
                        await modbusValueExecutionService
                            .WriteValue(modbusConfig, ModbusValueType.UInt, registerWrite.Address, registerWrite.Value, false)
                            .ConfigureAwait(false);
                    }
                },
                //The inverter falls back to its default behavior when the external setpoints are not refreshed
                //within this interval, which also acts as failsafe in case TSC crashes.
                TimeSpan.FromSeconds(60)));
        }
        return result;
    }

    public static List<(int Address, decimal Value)> GetRegisterWrites(HomeBatteryMode mode, int maxChargePowerW, int maxDischargePowerW)
    {
        return mode switch
        {
            HomeBatteryMode.Normal => new()
            {
                (OperatingModeAddress, OperatingModeDefault),
                (MinBatteryChargePowerAddress, 0),
                (MaxBatteryChargePowerAddress, maxChargePowerW),
                (MinBatteryDischargePowerAddress, 0),
                (MaxBatteryDischargePowerAddress, maxDischargePowerW),
                (GridPowerSetpointAddress, 0),
            },
            HomeBatteryMode.Hold => new()
            {
                (OperatingModeAddress, OperatingModeDefault),
                (MinBatteryChargePowerAddress, 0),
                (MaxBatteryChargePowerAddress, maxChargePowerW),
                (MinBatteryDischargePowerAddress, 0),
                (MaxBatteryDischargePowerAddress, 0),
                (GridPowerSetpointAddress, 0),
            },
            HomeBatteryMode.Charge => new()
            {
                (OperatingModeAddress, OperatingModeBatteryCharge),
                (MinBatteryChargePowerAddress, maxChargePowerW),
                (MaxBatteryChargePowerAddress, maxChargePowerW),
                (MinBatteryDischargePowerAddress, 0),
                (MaxBatteryDischargePowerAddress, 0),
                (GridPowerSetpointAddress, 0),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode can not be written to SMA hybrid inverter"),
        };
    }
}
