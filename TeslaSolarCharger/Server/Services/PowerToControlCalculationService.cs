using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
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

    public async Task<int> CalculatePowerToControl(List<DtoLoadPointWithCurrentChargingValues> chargingLoadPoints,
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
        if (availablePowerSources.InverterPowerAvailable || availablePowerSources.GridPowerAvailable || availablePowerSources.HomeBatteryPowerAvailable)
        {
            var pvValuesAge = _settings.LastPvValueUpdate;
            foreach (var chargingLoadPoint in chargingLoadPoints)
            {
                if (HasTooLateChanges(chargingLoadPoint, pvValuesAge, pvValuesAge))
                {
                    var dummyPower = chargingLoadPoints.Sum(c => c.ChargingPower);
                    _logger.LogWarning("Use {dummyPower}W as power to control due to too old solar values {pvValuesAge}", dummyPower, pvValuesAge);
                    notChargingWithExpectedPowerReasonHelper.AddGenericReason(new("Solar values are too old"));
                    return dummyPower;
                }
            }
        }
        

        var buffer = _configurationWrapper.PowerBuffer();
        _logger.LogDebug("Adding powerbuffer {powerbuffer}", buffer);
        if (buffer != 0)
        {
            notChargingWithExpectedPowerReasonHelper.AddGenericReason(new($"Charging speed is {(buffer > 0 ? "decreased" : "increased")} due to power buffer being set to {buffer}W"));
        }
        var averagedOverage = _settings.Overage ?? _constants.DefaultOverage;
        _logger.LogDebug("Averaged overage {averagedOverage}", averagedOverage);

        var currentChargingPower = chargingLoadPoints.Select(l => l.ChargingPower).Sum();
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
        if (availablePowerSources.HomeBatteryPowerAvailable)
        {
            overage = AddHomeBatteryStateToPowerCalculation(overage, notChargingWithExpectedPowerReasonHelper);
        }
        return overage + currentChargingPower;
    }

    public bool HasTooLateChanges(DtoLoadPointWithCurrentChargingValues chargingLoadPoint, DateTimeOffset earliestAmpChange,
    DateTimeOffset earliestPlugin)
    {
        _logger.LogTrace("{method}(({carId}, {connectorId}), {earliestAmpChange}, {earliestPlugin})",
            nameof(HasTooLateChanges), chargingLoadPoint.CarId, chargingLoadPoint.ChargingConnectorId, earliestAmpChange, earliestPlugin);
        if (chargingLoadPoint.CarId != default)
        {
            var car = _settings.Cars.First(c => c.Id == chargingLoadPoint.CarId.Value);
            var lastAmpChange = car.LastSetAmp.LastChanged;
            if (lastAmpChange > earliestAmpChange)
            {
                _logger.LogTrace("Car {carId}'s last amp change {lastAmpChange} is newer than {skipValueChanges}.", chargingLoadPoint.CarId, car.LastSetAmp.LastChanged, earliestAmpChange);
                return true;
            }

            var lastPlugIn = car.PluggedIn.LastChanged;
            if ((car.PluggedIn.Value == true) && (lastPlugIn > earliestPlugin) && (car.ChargerRequestedCurrent.Value > car.ChargerActualCurrent.Value))
            {
                _logger.LogTrace("Car {carId} was plugged in after {earliestPlugIn}.", chargingLoadPoint.CarId, earliestPlugin);
                return true;
            }
        }

        if (chargingLoadPoint.ChargingConnectorId != default)
        {
            var connectorState = _settings.OcppConnectorStates.GetValueOrDefault(chargingLoadPoint.ChargingConnectorId.Value);
            if (connectorState != default)
            {
                if (connectorState.LastSetCurrent.LastChanged > earliestAmpChange)
                {
                    _logger.LogTrace("Charging Connector {chargingConnectorId}'s last amp change {lastAmpChange} is newer than {skipValueChanges}.", chargingLoadPoint.ChargingConnectorId, connectorState.LastSetCurrent.LastChanged, earliestAmpChange);
                    return true;
                }

                if (connectorState.IsPluggedIn.Value
                    && (connectorState.IsPluggedIn.LastChanged > earliestPlugin)
                    && (connectorState.ChargingCurrent.Value > 0)
                    && (connectorState.ChargingCurrent.Value < connectorState.LastSetCurrent.Value))
                {
                    _logger.LogTrace("Charging Connector {chargingConnectorId} was plugged in after {earliestPlugin}.", chargingLoadPoint.ChargingConnectorId, earliestPlugin);
                    return true;
                }
            }
        }

        return false;
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

    public int GetBatteryTargetChargingPower()
    {
        _logger.LogTrace("{method}()", nameof(GetBatteryTargetChargingPower));
        var homeBatteryMinSoc = _configurationWrapper.HomeBatteryMinSoc();
        if (homeBatteryMinSoc == default)
        {
            return 0;
        }
        var homeBatteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
        if (homeBatteryMaxChargingPower == default)
        {
            return 0;
        }
        var actualHomeBatterySoc = _settings.HomeBatterySoc;
        return actualHomeBatterySoc < homeBatteryMinSoc ? homeBatteryMaxChargingPower.Value : 0;
    }

    private int GetBatteryTargetChargingPower(int? minSoc,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper)
    {
        var actualHomeBatterySoc = _settings.HomeBatterySoc;
        var homeBatteryMinSoc = minSoc;
        var homeBatteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
        if (actualHomeBatterySoc < homeBatteryMinSoc)
        {
            notChargingWithExpectedPowerReasonHelper.AddGenericReason(
                new NotChargingWithExpectedPowerReasonTemplate("Reserved {0}W for Home battery charging as its SOC ({1}%) is below minimum SOC ({2}%)",
                    homeBatteryMaxChargingPower ?? 0,
                    actualHomeBatterySoc,
                    homeBatteryMinSoc));
            return homeBatteryMaxChargingPower ?? 0;
        }

        return 0;
    }
}
