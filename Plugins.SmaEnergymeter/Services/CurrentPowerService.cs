using Plugins.SmaEnergymeter.Dtos;

namespace Plugins.SmaEnergymeter.Services;

public class CurrentPowerService
{
    private readonly ILogger<CurrentPowerService> _logger;
    private readonly SharedValues _sharedValues;

    public CurrentPowerService(ILogger<CurrentPowerService> logger, SharedValues sharedValues)
    {
        _logger = logger;
        _sharedValues = sharedValues;
    }

    public int GetCurrentPower(uint? serialNumber)
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentPower));
        var energyMeterValue = GetEnergyMeterValue(serialNumber);
        return energyMeterValue.OverageW;
    }

    

    public DtoEnergyMeterValue GetAllValues(uint? serialNumber)
    {
        _logger.LogTrace("{method}()", nameof(GetAllValues));
        var energyMeterValue = GetEnergyMeterValue(serialNumber);
        return energyMeterValue;
    }

    private DtoEnergyMeterValue GetEnergyMeterValue(uint? serialNumber)
    {
        var energyMeterValue = serialNumber.HasValue
            ? _sharedValues.EnergyMeterValues[serialNumber.Value]
            : _sharedValues.EnergyMeterValues.Last().Value;
        return energyMeterValue;
    }
}
