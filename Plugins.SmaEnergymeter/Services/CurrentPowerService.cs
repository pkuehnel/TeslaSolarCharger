using Plugins.SmaEnergymeter.Dtos;

namespace Plugins.SmaEnergymeter.Services;

public class CurrentPowerService
{
    /// <summary>
    /// The energy meter sends its values every second, so older values mean the multicast stopped arriving.
    /// </summary>
    public static readonly TimeSpan MaxValueAge = TimeSpan.FromSeconds(10);

    private readonly ILogger<CurrentPowerService> _logger;
    private readonly SharedValues _sharedValues;
    private readonly TimeProvider _timeProvider;

    public CurrentPowerService(ILogger<CurrentPowerService> logger, SharedValues sharedValues, TimeProvider timeProvider)
    {
        _logger = logger;
        _sharedValues = sharedValues;
        _timeProvider = timeProvider;
    }

    /// <returns>The current overage or null if there are no current values.</returns>
    public int? GetCurrentPower(uint? serialNumber)
    {
        _logger.LogTrace("{method}({serialNumber})", nameof(GetCurrentPower), serialNumber);
        return GetAllValues(serialNumber)?.OverageW;
    }

    /// <returns>The values of the energy meter or null if none were received within <see cref="MaxValueAge"/>.</returns>
    public DtoEnergyMeterValue? GetAllValues(uint? serialNumber)
    {
        _logger.LogTrace("{method}({serialNumber})", nameof(GetAllValues), serialNumber);
        var energyMeterValue = GetEnergyMeterValue(serialNumber);
        if (energyMeterValue == default)
        {
            _logger.LogWarning("No values received from energy meter {serialNumber}", serialNumber);
            return null;
        }
        var age = _timeProvider.GetUtcNow().UtcDateTime - energyMeterValue.LastValuesFrom;
        if (age > MaxValueAge)
        {
            _logger.LogWarning("Last values of energy meter {serialNumber} are from {lastValuesFrom} UTC, which is too old",
                serialNumber, energyMeterValue.LastValuesFrom);
            return null;
        }
        return energyMeterValue;
    }

    private DtoEnergyMeterValue? GetEnergyMeterValue(uint? serialNumber)
    {
        if (serialNumber.HasValue)
        {
            return _sharedValues.EnergyMeterValues.GetValueOrDefault(serialNumber.Value);
        }
        return _sharedValues.EnergyMeterValues.Count == 0 ? null : _sharedValues.EnergyMeterValues.Last().Value;
    }
}
