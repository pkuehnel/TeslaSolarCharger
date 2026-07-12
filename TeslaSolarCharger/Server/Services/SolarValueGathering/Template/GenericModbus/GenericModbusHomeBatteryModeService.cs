using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Modbus.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericModbus;

/// <summary>
/// Home battery control for all vendors defined via register maps in <see cref="ModbusTemplateDefinitions"/>
/// that include a battery control definition.
/// </summary>
public class GenericModbusHomeBatteryModeService : IHomeBatteryModeSetupService
{
    private const int FallbackMinSoc = 10;

    private readonly ILogger<GenericModbusHomeBatteryModeService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public GenericModbusHomeBatteryModeService(ILogger<GenericModbusHomeBatteryModeService> logger,
        IServiceScopeFactory serviceScopeFactory, ITemplateValueConfigurationService templateValueConfigurationService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _templateValueConfigurationService = templateValueConfigurationService;
    }

    public async Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(GetControllersAsync));
        var gatherTypes = ModbusTemplateDefinitions.Definitions
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
            var typedConfig = config.Configuration.ToObject<DtoGenericModbusTemplateValueConfiguration>();
            if (typedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (!typedConfig.EnableHomeBatteryControl)
            {
                continue;
            }
            if (string.IsNullOrEmpty(typedConfig.Host))
            {
                _logger.LogError("Host for template configuration ID {id} is null or empty.", config.Id);
                continue;
            }
            var definition = ModbusTemplateDefinitions.Definitions[config.GatherType.Value];
            var batteryControl = definition.BatteryControl!;
            var modbusConfiguration = GenericModbusTemplateValueSetupService.CreateModbusConfiguration(config.Id, typedConfig, definition);
            var maxChargePowerW = typedConfig.MaxBatteryChargePowerW;
            var maxDischargePowerW = typedConfig.MaxBatteryDischargePowerW;
            var serviceScopeFactory = _serviceScopeFactory;
            result.Add(new DtoHomeBatteryModeController(config.Id, config.Name ?? string.Empty,
                async (mode, ct) =>
                {
                    using var executionScope = serviceScopeFactory.CreateScope();
                    var settings = executionScope.ServiceProvider.GetRequiredService<ISettings>();
                    var configurationWrapper = executionScope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
                    var minSoc = configurationWrapper.HomeBatteryMinSoc() ?? FallbackMinSoc;
                    var maxChargeSoc = configurationWrapper.HomeBatteryMaxChargeSoc();
                    var modbusValueExecutionService = executionScope.ServiceProvider
                        .GetRequiredService<IModbusValueExecutionService>();
                    foreach (var write in batteryControl.GetWrites(mode))
                    {
                        ct.ThrowIfCancellationRequested();
                        var value = ResolveWriteValue(write, maxChargePowerW, maxDischargePowerW,
                            settings.HomeBatterySoc, minSoc, maxChargeSoc);
                        await modbusValueExecutionService
                            .WriteValue(modbusConfiguration, write.ValueType, write.Address, value, write.WriteFunction, false)
                            .ConfigureAwait(false);
                    }
                },
                batteryControl.RewriteInterval));
        }
        return result;
    }

    public static decimal ResolveWriteValue(ModbusBatteryModeWrite write, int maxChargePowerW, int maxDischargePowerW,
        int? currentSoc, int minSoc, int maxChargeSoc)
    {
        var rawValue = write.Source switch
        {
            BatteryModeWriteValueSource.Constant => write.ConstantValue,
            BatteryModeWriteValueSource.MaxChargePowerW => maxChargePowerW,
            BatteryModeWriteValueSource.MaxDischargePowerW => maxDischargePowerW,
            BatteryModeWriteValueSource.MinSoc => minSoc,
            BatteryModeWriteValueSource.MaxChargeSoc => maxChargeSoc,
            BatteryModeWriteValueSource.CurrentSoc => Math.Min(100, Math.Max(
                currentSoc ?? throw new InvalidOperationException("Home battery soc is required to write the current soc"),
                minSoc)),
            BatteryModeWriteValueSource.Random => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 32000),
            _ => throw new ArgumentOutOfRangeException(nameof(write), write.Source, "Unknown battery mode write value source"),
        };
        return Math.Round(rawValue * write.Factor);
    }
}
