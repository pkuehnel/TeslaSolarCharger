using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class TokenRefreshJob : IJob
{
    private readonly ILogger<TokenRefreshJob> _logger;
    private readonly IBackendApiService _backendApiService;
    private readonly ITeslaFleetApiService _teslaFleetApiService;

    public TokenRefreshJob(ILogger<TokenRefreshJob> logger,
        IBackendApiService backendApiService, ITeslaFleetApiService teslaFleetApiService)
    {
        _logger = logger;
        _backendApiService = backendApiService;
        _teslaFleetApiService = teslaFleetApiService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        try
        {
            await _backendApiService.RefreshBackendTokenIfNeeded().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not refresh backend token.");
        }
        try
        {
            await _teslaFleetApiService.RefreshFleetApiTokenIfNeeded().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not refresh fleet API token.");
        }
    }
}
