using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services;

public class PowerToControlCalculationService : IPowerToControlCalculationService
{
    private readonly ILogger<PowerToControlCalculationService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IConstants _constants;

    public PowerToControlCalculationService(ILogger<PowerToControlCalculationService> logger,
        ITeslaSolarChargerContext context,
        ISettings settings,
        IConfigurationWrapper configurationWrapper,
        IConstants constants)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _constants = constants;
    }

    public async Task<int> CalculatePowerToControl(int currentChargingPower,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(CalculatePowerToControl));
        var resultConfigurations = await _context.ModbusResultConfigurations.Select(r => r.UsedFor).ToListAsync(cancellationToken: cancellationToken);
        resultConfigurations.AddRange(await _context.RestValueResultConfigurations.Select(r => r.UsedFor).ToListAsync(cancellationToken: cancellationToken));
        resultConfigurations.AddRange(await _context.MqttResultConfigurations.Select(r => r.UsedFor).ToListAsync(cancellationToken: cancellationToken));
        var availablePowerSources = new DtoAvailablePowerSources()
        {
            InverterPowerAvailable = resultConfigurations.Any(c => c == ValueUsage.InverterPower),
            GridPowerAvailable = resultConfigurations.Any(c => c == ValueUsage.GridPower),
            HomeBatteryPowerAvailable = resultConfigurations.Any(c => c == ValueUsage.HomeBatteryPower),
        };

        var buffer = _configurationWrapper.PowerBuffer();
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);
        if (buffer != 0)
        {
            notChargingWithExpectedPowerReasonHelper.AddGenericReason(new($"Power buffer of {buffer} is added"));
        }
        var averagedOverage = _settings.Overage ?? _constants.DefaultOverage;
        _logger.LogDebug("Averaged overage {averagedOverage}", averagedOverage);

        if (!availablePowerSources.GridPowerAvailable
            && availablePowerSources.InverterPowerAvailable)
        {
            _logger.LogDebug("Using Inverter power {inverterPower} minus current combined charging power {chargingPowerAtHome} as overage",
                _settings.InverterPower, currentChargingPower);
            if (_settings.InverterPower == default)
            {
                _logger.LogWarning("Inverter power is not available, can not calculate power to control.");
                return 0;
            }
            averagedOverage = _settings.InverterPower.Value - currentChargingPower;
        }
        var overage = averagedOverage - buffer;
        _logger.LogDebug("Calculated overage {overage} after subtracting power buffer ({buffer})", overage, buffer);

        overage = AddHomeBatteryStateToPowerCalculation(overage, notChargingWithExpectedPowerReasonHelper);
        return overage + currentChargingPower;
    }

    private int AddHomeBatteryStateToPowerCalculation(int overage,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper)
    {
        var homeBatteryMinSoc = _configurationWrapper.HomeBatteryMinSoc();
        _logger.LogDebug("Home battery min soc: {homeBatteryMinSoc}", homeBatteryMinSoc);
        var homeBatteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
        _logger.LogDebug("Home battery should charging power: {homeBatteryMaxChargingPower}", homeBatteryMaxChargingPower);
        if (homeBatteryMinSoc == default || homeBatteryMaxChargingPower == default)
        {
            return overage;
        }
        var batteryMinChargingPower = GetBatteryTargetChargingPower(homeBatteryMinSoc, notChargingWithExpectedPowerReasonHelper);
        var actualHomeBatterySoc = _settings.HomeBatterySoc;
        _logger.LogDebug("Home battery actual soc: {actualHomeBatterySoc}", actualHomeBatterySoc);
        var actualHomeBatteryPower = _settings.HomeBatteryPower;
        _logger.LogDebug("Home battery actual power: {actualHomeBatteryPower}", actualHomeBatteryPower);
        if (actualHomeBatteryPower == default)
        {
            return overage;
        }
        var overageToIncrease = actualHomeBatteryPower.Value - batteryMinChargingPower;
        overage += overageToIncrease;
        var inverterAcOverload = (_configurationWrapper.MaxInverterAcPower() - _settings.InverterPower) * (-1);
        if (inverterAcOverload > 0)
        {
            _logger.LogDebug("As inverter power is higher than max inverter AC power, overage is reduced by overload");
            overage -= (inverterAcOverload.Value - batteryMinChargingPower);
        }
        return overage;
    }

    private int GetBatteryTargetChargingPower(int? minSoc,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper)
    {
        var actualHomeBatterySoc = _settings.HomeBatterySoc;
        var homeBatteryMinSoc = minSoc;
        var homeBatteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
        if (actualHomeBatterySoc < homeBatteryMinSoc)
        {
            notChargingWithExpectedPowerReasonHelper.AddGenericReason(new($"Reserved {homeBatteryMaxChargingPower}W for Home battery charging as its SOC ({actualHomeBatterySoc}) is below minimum SOC ({homeBatteryMinSoc})"));
            return homeBatteryMaxChargingPower ?? 0;
        }

        return 0;
    }
}
