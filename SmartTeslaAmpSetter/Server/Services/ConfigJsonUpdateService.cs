using System.Reflection;
using Newtonsoft.Json;
using SmartTeslaAmpSetter.Shared.Dtos;

namespace SmartTeslaAmpSetter.Server.Services;

public class ConfigJsonUpdateService
{
    private readonly ILogger<ConfigJsonUpdateService> _logger;
    private readonly Settings _settings;
    private readonly IConfiguration _configuration;

    public ConfigJsonUpdateService(ILogger<ConfigJsonUpdateService> logger, Settings settings, IConfiguration configuration)
    {
        _logger = logger;
        _settings = settings;
        _configuration = configuration;
    }

    public async Task UpdateConfigJson()
    {
        _logger.LogTrace("{method}()", nameof(UpdateConfigJson));
        var configFileLocation = _configuration.GetValue<string>("ConfigFileLocation");
        if (_settings.Cars.Any(c => c.UpdatedSincLastWrite))
        {
            _logger.LogDebug("Update configuration.json");
            foreach (var settingsCar in _settings.Cars)
            {
                settingsCar.UpdatedSincLastWrite = false;
            }

            var path = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName;
            path = Path.Combine(path, configFileLocation);
            var fileInfo = new FileInfo(path);
            if (!Directory.Exists(fileInfo.Directory.FullName))
            {
                Directory.CreateDirectory(fileInfo.Directory.FullName);
            }
            var json = JsonConvert.SerializeObject(_settings.Cars);
            await File.WriteAllTextAsync(path, json);
        }
    }
}