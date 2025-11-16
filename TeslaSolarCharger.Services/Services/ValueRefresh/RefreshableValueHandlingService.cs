using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Services.Services.ValueRefresh;

public class RefreshableValueHandlingService : GenericValueHandlingServiceBase<IRefreshableValue<decimal>, decimal, int>,
    IRefreshableValueHandlingService, IDecimalValueHandlingService
{
    private readonly ILogger<RefreshableValueHandlingService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;


    public RefreshableValueHandlingService(
        ILogger<RefreshableValueHandlingService> logger,
        IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task RefreshValues()
    {
        _logger.LogTrace("{method}()", nameof(RefreshValues));
        using var scope = _serviceScopeFactory.CreateScope();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.DateTimeOffSetUtcNow();

        // snapshot to avoid modification during enumeration
        var refreshables = GetGenericValuesSnapshot();

        var tasks = refreshables
            .Where(r => !r.IsExecuting && (r.NextExecution == null || r.NextExecution <= now))
            .Select(r => r.RefreshValueAsync(CancellationToken.None));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public override async Task RecreateValues(ConfigurationType? configurationType, params List<int> configurationIds)
    {
        _logger.LogTrace("{method}()", nameof(RecreateValues));

        // 1) Request cancellation for any in-flight refresh
        var refreshablesSnapshot = GetGenericValuesSnapshot();

        var refreshablesToCancel = refreshablesSnapshot
            .Where(r => configurationType == default || r.SourceValueKey.ConfigurationType == configurationType)
            .Where(r => configurationIds.Count == 0 || configurationIds.Contains(r.SourceValueKey.SourceId))
            .ToList();
        foreach (var refreshable in refreshablesToCancel)
        {
            refreshable.Cancel();
        }

        // 2) Await completion of any running tasks that need to be canceled
        var running = refreshablesToCancel
            .Select(r => r.RunningTask)
            .Where(t => t is not null)
            .Cast<Task>()
            .ToArray();

        if (running.Length > 0)
        {
            try
            {
                await Task.WhenAll(running).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling; swallow
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "One or more refresh tasks failed while cancelling during {method}.", nameof(RecreateValues));
            }
        }

        await RemoveValuesAsync(refreshablesToCancel);

        using var setupScope = _serviceScopeFactory.CreateScope();
        var configurationWrapper = setupScope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
        var solarValueRefreshInterval = configurationWrapper.PvValueJobUpdateIntervall();

        var refreshableValueSetupServices = setupScope.ServiceProvider.GetServices<IRefreshableValueSetupService>();
        var newRefreshables = new List<DelegateRefreshableValue<decimal>>();
        foreach (var refreshableValueSetupService in refreshableValueSetupServices)
        {
            if (configurationType != default && refreshableValueSetupService.ConfigurationType != configurationType)
            {
                continue;
            }
            _logger.LogTrace("Gather refreshables for type {type}", refreshableValueSetupService.GetType().Name);
            var refreshables = await refreshableValueSetupService.GetDecimalRefreshableValuesAsync(solarValueRefreshInterval, configurationIds);
            _logger.LogTrace("Got {count} refreshables", refreshables.Count);
            newRefreshables.AddRange(refreshables);
        }
        AddGenericValues(newRefreshables);
    }
}
