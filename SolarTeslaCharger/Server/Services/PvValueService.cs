using SolarTeslaCharger.Server.Contracts;
using SolarTeslaCharger.Shared.Contracts;
using SolarTeslaCharger.Shared.Dtos.Contracts;

namespace SolarTeslaCharger.Server.Services;

public class PvValueService : IPvValueService
{
    private readonly ILogger<PvValueService> _logger;
    private readonly ISettings _settings;
    private readonly IGridService _gridService;
    private readonly IInMemoryValues _inMemoryValues;
    private readonly IConfigurationWrapper _configurationWrapper;

    public PvValueService(ILogger<PvValueService> logger, ISettings settings, IGridService gridService,
        IInMemoryValues inMemoryValues, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _settings = settings;
        _gridService = gridService;
        _inMemoryValues = inMemoryValues;
        _configurationWrapper = configurationWrapper;
    }

    public async Task UpdatePvValues()
    {
        _logger.LogTrace("{method}()", nameof(UpdatePvValues));

        var overage = await _gridService.GetCurrentOverage().ConfigureAwait(false);
        _logger.LogDebug("Overage is {overage}", overage);
        _settings.Overage = overage;
        if (overage != null)
        {
            AddOverageValueToInMemoryList((int)overage);
        }
        _settings.InverterPower = await _gridService.GetCurrentInverterPower().ConfigureAwait(false);
    }

    public int GetAveragedOverage()
    {
        _logger.LogTrace("{method}()", nameof(GetAveragedOverage));
        long weightedSum = 0;
        _logger.LogDebug("Build weighted average of {count} values", _inMemoryValues.OverageValues.Count);
        for (var i = 0; i < _inMemoryValues.OverageValues.Count; i++)
        {
            _logger.LogTrace("Power Value: {value}", _inMemoryValues.OverageValues[i]);
            weightedSum += _inMemoryValues.OverageValues[i] * (i + 1);
            _logger.LogTrace("weightedSum: {value}", weightedSum);
        }
        var weightedCount = _inMemoryValues.OverageValues.Count * (_inMemoryValues.OverageValues.Count + 1) / 2;
        if (weightedCount == 0)
        {
            throw new InvalidOperationException("There are no power values available");
        }
        return (int)(weightedSum / weightedCount);
    }

    private void AddOverageValueToInMemoryList(int overage)
    {
        _logger.LogTrace("{method}({overage})", nameof(AddOverageValueToInMemoryList), overage);
        _inMemoryValues.OverageValues.Add(overage);

        var valuesToSave = (int)(_configurationWrapper.ChargingValueJobUpdateIntervall().TotalSeconds /
                            _configurationWrapper.PvValueJobUpdateIntervall().TotalSeconds);

        if (_inMemoryValues.OverageValues.Count > valuesToSave)
        {
            _inMemoryValues.OverageValues.RemoveRange(0, _inMemoryValues.OverageValues.Count - valuesToSave);
        }
    }
}