using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TscConfigurationService(
    ILogger<TscConfigurationService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IConstants constants)
    : ITscConfigurationService
{
    public async Task<Guid> GetInstallationId()
    {
        logger.LogTrace("{method}()", nameof(GetInstallationId));
        var configurationIdString = teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.InstallationIdKey)
            .Select(c => c.Value)
            .FirstOrDefault();

        if (configurationIdString != default)
        {
            return Guid.Parse(configurationIdString);
        }

        var installationIdConfiguration = new TscConfiguration()
        {
            Key = constants.InstallationIdKey,
            Value = Guid.NewGuid().ToString(),
        };
        teslaSolarChargerContext.TscConfigurations.Add(installationIdConfiguration);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        return Guid.Parse(installationIdConfiguration.Value);
    }

    /// <summary>
    /// Get a configuration value by its key
    /// </summary>
    /// <param name="configurationKey">Configuration key to get the value from</param>
    /// <returns>Configuration value, null if value key does not exist in database</returns>
    public async Task<string?> GetConfigurationValueByKey(string configurationKey)
    {
        logger.LogTrace("{method}({configurationKey})", nameof(GetConfigurationValueByKey), configurationKey);
        var configurationValue = await teslaSolarChargerContext.TscConfigurations
            .FirstOrDefaultAsync(c => c.Key == constants.InstallationIdKey);

        return configurationValue?.Value;
    }

    /// <summary>
    /// Set a configuration value by its key. If the key does not exist, it will be created. If it exists, the value will be updated.
    /// </summary>
    /// <param name="configurationKey">Key to update</param>
    /// <param name="configurationValue">Value to update</param>
    /// <returns></returns>
    public async Task SetConfigurationValueByKey(string configurationKey, string configurationValue)
    {
        logger.LogTrace("{method}({configurationKey}, {configurationValue})", nameof(SetConfigurationValueByKey), configurationKey, configurationValue);
        var configuration = await teslaSolarChargerContext.TscConfigurations
            .FirstOrDefaultAsync(c => c.Key == configurationKey);

        if (configuration == default)
        {
            configuration = new()
            {
                Key = configurationKey,
                Value = configurationValue,
            };
            teslaSolarChargerContext.TscConfigurations.Add(configuration);
        }
        else
        {
            configuration.Value = configurationValue;
        }

        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }
}
