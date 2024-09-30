using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TeslaMateDbContextWrapper(ILogger<TeslaMateDbContextWrapper> logger,
    IServiceProvider serviceProvider,
    IDbConnectionStringHelper dbConnectionStringHelper) : ITeslaMateDbContextWrapper
{
    public ITeslamateContext? GetTeslaMateContextIfAvailable()
    {
        logger.LogTrace("{method}()", nameof(GetTeslaMateContextIfAvailable));
        var connectionString = dbConnectionStringHelper.GetTeslaMateConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            logger.LogDebug("No TeslaMate connection string available.");
            return null;
        }

        var teslaMateContext = serviceProvider.GetRequiredService<ITeslamateContext>();
        return teslaMateContext;
    }
}
