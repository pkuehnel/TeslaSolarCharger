using SmartTeslaAmpSetter.Server.Contracts;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;

namespace SmartTeslaAmpSetter.Server.Services;

public class PvValueService : IPvValueService
{
    private readonly ILogger<PvValueService> _logger;
    private readonly ISettings _settings;
    private readonly IGridService _gridService;

    public PvValueService(ILogger<PvValueService> logger, ISettings settings, IGridService gridService)
    {
        _logger = logger;
        _settings = settings;
        _gridService = gridService;
    }

    public async Task UpdatePvValues()
    {
        _logger.LogTrace("{method}()", nameof(UpdatePvValues));

        _settings.Overage = await _gridService.GetCurrentOverage().ConfigureAwait(false);
        _settings.InverterPower = await _gridService.GetCurrentInverterPower().ConfigureAwait(false);
    }
}