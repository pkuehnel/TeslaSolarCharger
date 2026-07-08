using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.SunSpec;

/// <summary>
/// Home battery control for all vendors defined via SunSpec maps in <see cref="SunSpecTemplateDefinitions"/> that
/// include a battery control definition. Supports model 124 point writes and plain vendor register writes.
/// </summary>
public class GenericSunSpecHomeBatteryModeService : IHomeBatteryModeSetupService
{
    private readonly ILogger<GenericSunSpecHomeBatteryModeService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public GenericSunSpecHomeBatteryModeService(ILogger<GenericSunSpecHomeBatteryModeService> logger,
        IServiceScopeFactory serviceScopeFactory, ITemplateValueConfigurationService templateValueConfigurationService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _templateValueConfigurationService = templateValueConfigurationService;
    }

    public async Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(GetControllersAsync));
        var gatherTypes = SunSpecTemplateDefinitions.Definitions
            .Where(d => d.Value.BatteryControl != default)
            .Select(d => d.Key)
            .ToList();
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => gatherTypes.Contains(c.GatherType);
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var result = new List<DtoHomeBatteryModeController>();
        foreach (var config in configs)
        {
            if (config.Configuration == default || config.GatherType == default)
            {
                continue;
            }
            var typedConfig = config.Configuration.ToObject<DtoSunSpecTemplateValueConfiguration>();
            if (typedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (!typedConfig.EnableHomeBatteryControl || string.IsNullOrEmpty(typedConfig.Host))
            {
                continue;
            }
            var definition = SunSpecTemplateDefinitions.Definitions[config.GatherType.Value];
            var batteryControl = definition.BatteryControl!;
            var modbusConfiguration = GenericSunSpecTemplateValueSetupService.CreateModbusConfiguration(config.Id, typedConfig);
            var maxChargeRatePercent = typedConfig.MaxChargeRatePercent;
            var maxChargePowerW = typedConfig.MaxBatteryChargePowerW;
            var serviceScopeFactory = _serviceScopeFactory;
            result.Add(new DtoHomeBatteryModeController(config.Id, config.Name ?? string.Empty,
                async (mode, ct) =>
                {
                    using var executionScope = serviceScopeFactory.CreateScope();
                    var sunSpecClient = executionScope.ServiceProvider.GetRequiredService<ISunSpecClient>();
                    var modbusValueExecutionService = executionScope.ServiceProvider.GetRequiredService<IModbusValueExecutionService>();
                    foreach (var write in batteryControl.GetWrites(mode))
                    {
                        ct.ThrowIfCancellationRequested();
                        var value = ResolveWriteValue(write, maxChargeRatePercent, maxChargePowerW);
                        if (write.SunSpecPointReference != default)
                        {
                            await sunSpecClient.WriteValueAsync(modbusConfiguration, write.SunSpecPointReference, value,
                                write.WriteFunction, ct).ConfigureAwait(false);
                        }
                        else if (write.PlainRegisterAddress != default)
                        {
                            //Plain vendor register writes may use a different endianess than the SunSpec reads
                            var plainConfiguration = GenericSunSpecTemplateValueSetupService.CreateModbusConfiguration(config.Id, typedConfig);
                            plainConfiguration.Endianess = write.PlainRegisterEndianess;
                            await modbusValueExecutionService.WriteValue(plainConfiguration, write.PlainRegisterValueType,
                                write.PlainRegisterAddress.Value, value, write.WriteFunction, false).ConfigureAwait(false);
                        }
                    }
                },
                batteryControl.RewriteInterval));
        }
        return result;
    }

    public static decimal ResolveWriteValue(SunSpecBatteryModeWrite write, int maxChargeRatePercent, int maxChargePowerW)
    {
        return write.Source switch
        {
            SunSpecWriteValueSource.Constant => write.ConstantValue,
            SunSpecWriteValueSource.NegativeMaxChargeRatePercent => -maxChargeRatePercent,
            SunSpecWriteValueSource.NegativeMaxChargePowerW => -maxChargePowerW,
            _ => throw new ArgumentOutOfRangeException(nameof(write), write.Source, "Unknown SunSpec write value source"),
        };
    }
}
