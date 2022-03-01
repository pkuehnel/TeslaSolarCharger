using SmartTeslaAmpSetter.Shared.Dtos;

namespace SmartTeslaAmpSetter.Server.Services;

public class ConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly Settings _settings;

    public ConfigService(ILogger<ConfigService> logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public Settings GetSettings()
    {
        _logger.LogTrace("{method}()", nameof(GetSettings));
        return _settings;
    }
}