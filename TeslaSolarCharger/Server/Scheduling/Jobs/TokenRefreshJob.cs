using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class TokenRefreshJob : IJob
{
    private readonly ILogger<TokenRefreshJob> _logger;
    private readonly IBackendApiService _backendApiService;
    private readonly ITeslaFleetApiService _teslaFleetApiService;
    private readonly ISmartCarApiService _smartCarApiService;

    public TokenRefreshJob(ILogger<TokenRefreshJob> logger,
        IBackendApiService backendApiService, ITeslaFleetApiService teslaFleetApiService, ISmartCarApiService smartCarApiService)
    {
        _logger = logger;
        _backendApiService = backendApiService;
        _teslaFleetApiService = teslaFleetApiService;
        _smartCarApiService = smartCarApiService;
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
            await _teslaFleetApiService.RefreshFleetApiTokenIfRequired().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not refresh fleet API token.");
        }
        try
        {
            await _smartCarApiService.UpdateSmartCarCarTypes().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not refresh SmartCar API token.");
        }
    }
}
