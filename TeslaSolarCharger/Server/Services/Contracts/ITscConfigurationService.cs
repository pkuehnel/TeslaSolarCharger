namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITscConfigurationService
{
    Task<Guid> GetInstallationId();

    /// <summary>
    /// Get a configuration value by its key
    /// </summary>
    /// <param name="configurationKey">Configuration key to get the value from</param>
    /// <returns>Configuration value, null if value key does not exist in database</returns>
    Task<string?> GetConfigurationValueByKey(string configurationKey);

    /// <summary>
    /// Set a configuration value by its key. If the key does not exist, it will be created. If it exists, the value will be updated.
    /// </summary>
    /// <param name="configurationKey">Key to update</param>
    /// <param name="configurationValue">Value to update</param>
    /// <returns></returns>
    Task SetConfigurationValueByKey(string configurationKey, string configurationValue);
}
