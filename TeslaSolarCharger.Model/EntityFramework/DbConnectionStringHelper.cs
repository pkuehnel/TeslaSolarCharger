using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Model.EntityFramework;

public class DbConnectionStringHelper : IDbConnectionStringHelper
{
    private readonly ILogger<DbConnectionStringHelper> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;

    public DbConnectionStringHelper(ILogger<DbConnectionStringHelper> logger, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
    }

    public string GetTeslaMateConnectionString()
    {
        _logger.LogTrace("{method}()", nameof(GetTeslaMateConnectionString));
        var server = _configurationWrapper.TeslaMateDbServer();
        var port = _configurationWrapper.TeslaMateDbPort();
        var databaseName = _configurationWrapper.TeslaMateDbDatabaseName();
        var username = _configurationWrapper.TeslaMateDbUser();
        var password = _configurationWrapper.TeslaMateDbPassword();
        var connectionString = $"Host={server};Port={port};Database={databaseName};Username={username};Password={password}";
        _logger.LogTrace("ConnectionString: {connectionString}", connectionString);
        return connectionString;
    }

    public string GetTeslaSolarChargerDbPath()
    {
        _logger.LogTrace("{method}()", nameof(GetTeslaSolarChargerDbPath));
        var connectionString = $"Data Source={_configurationWrapper.SqliteFileFullName()};Pooling=False";
        return connectionString;
    }
}
