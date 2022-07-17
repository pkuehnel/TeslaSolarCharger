using System.Runtime.Caching;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Shared.Services;

public class BaseConfigurationService : IBaseConfigurationService
{
    private readonly ILogger<BaseConfigurationService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly string _baseConfigurationMemoryCacheName = "baseConfiguration";

    public BaseConfigurationService(ILogger<BaseConfigurationService> logger, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
    }

    public async Task<DtoBaseConfiguration> GetBaseConfiguration()
    {
        _logger.LogTrace("{method}()", nameof(GetBaseConfiguration));
        var jsonFileContent = await BaseConfigurationJsonFileContent();

        var dtoBaseConfiguration = JsonConvert.DeserializeObject<DtoBaseConfiguration>(jsonFileContent);

        if (dtoBaseConfiguration == null)
        {
            throw new ArgumentException($"Could not deserialize {jsonFileContent} to {nameof(DtoBaseConfiguration)}");
        }

        return dtoBaseConfiguration;
    }

    private async Task<string?> BaseConfigurationJsonFileContent()
    {
        var cache = MemoryCache.Default;
        var jsonFileContent = cache[_baseConfigurationMemoryCacheName] as string;
        if (jsonFileContent == null)
        {
            var filePath = _configurationWrapper.BaseConfigFileFullName();
            var cacheItemPolicy = new CacheItemPolicy();
            var filePathList = new List<string>()
            {
                filePath,
            };

            cacheItemPolicy.ChangeMonitors.Add(new HostFileChangeMonitor(filePathList));

            if (File.Exists(filePath))
            {
                jsonFileContent = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

                cache.Set(_baseConfigurationMemoryCacheName, jsonFileContent, cacheItemPolicy);
            }
        }

        return jsonFileContent;
    }

    public async Task SaveBaseConfiguration(DtoBaseConfiguration baseConfiguration)
    {
        var baseConfigurationBase = (BaseConfigurationBase)baseConfiguration;
        var baseConfigurationJson = JsonConvert.DeserializeObject<BaseConfigurationJson>(JsonConvert.SerializeObject(baseConfigurationBase));
        //if (false)
        //{
        //    baseConfigurationJson.LastEditDateTime = DateTime.UtcNow;
        //}
        var jsonFileContent = JsonConvert.SerializeObject(baseConfigurationJson);

        var configFileLocation = _configurationWrapper.BaseConfigFileFullName();
        var fileInfo = new FileInfo(configFileLocation);
        var configDirectoryFullName = fileInfo.Directory?.FullName;
        if (!Directory.Exists(configDirectoryFullName))
        {
            _logger.LogDebug("Config directory {directoryname} does not exist.", configDirectoryFullName);
            Directory.CreateDirectory(configDirectoryFullName ?? throw new InvalidOperationException());
        }

        await File.WriteAllTextAsync(configFileLocation, jsonFileContent);
    }

    public async Task<bool> IsBaseConfigurationJsonRelevant()
    {
        var jsonContent = await BaseConfigurationJsonFileContent().ConfigureAwait(false);
        if (jsonContent == null)
        {
            return false;
        }
        var baseConfigurationJson = JsonConvert.DeserializeObject<BaseConfigurationJson>(jsonContent);
        return baseConfigurationJson?.LastEditDateTime != null;
    }
}