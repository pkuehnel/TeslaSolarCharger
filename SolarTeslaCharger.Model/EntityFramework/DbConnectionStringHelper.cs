using Microsoft.Extensions.Logging;
using SolarTeslaCharger.Model.Contracts;
using SolarTeslaCharger.Shared.Contracts;

namespace SolarTeslaCharger.Model.EntityFramework;

public class DbConnectionStringHelper : IDbConnectionStringHelper
{
    private readonly ILogger<DbConnectionStringHelper> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;

    public DbConnectionStringHelper(ILogger<DbConnectionStringHelper> logger, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
    }

    public string GetConnectionString()
    {
        _logger.LogTrace("{method}()", nameof(GetConnectionString));
        var server = _configurationWrapper.TeslaMateDbServer();
        var port = _configurationWrapper.TeslaMateDbPort();
        var databaseName = _configurationWrapper.TeslaMateDbDatabaseName();
        var username = _configurationWrapper.TeslaMateDbUser();
        var password = _configurationWrapper.TeslaMateDbPassword();
        var connectionString = $"Host={server};Port={port};Database={databaseName};Username={username};Password={password}";
        _logger.LogTrace("ConnectionString: {connectionString}", connectionString);
        return connectionString;
    }
}