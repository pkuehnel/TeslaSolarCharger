using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.ValueRefresh;

public class RefreshableValueHandlingService : IRefreshableValueHandlingService, IGenericValueHandlingService
{
    private readonly ILogger<RefreshableValueHandlingService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HashSet<IRefreshableValue<decimal>> _refreshables = new();
    private readonly object _refreshablesLock = new();


    public RefreshableValueHandlingService(
        ILogger<RefreshableValueHandlingService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public IReadOnlyDictionary<ValueUsage, List<DtoHistoricValue<decimal>>> GetSolarValues(out bool hasErrors)
    {
        _logger.LogTrace("{method}()", nameof(GetSolarValues));
        var result = new Dictionary<ValueUsage, List<DtoHistoricValue<decimal>>>();
        var valueUsages = new HashSet<ValueUsage>
        {
            ValueUsage.InverterPower,
            ValueUsage.GridPower,
            ValueUsage.HomeBatteryPower,
            ValueUsage.HomeBatterySoc,
        };
        var encounteredError = false;

        var refreshablesSnapshot = GetRefreshablesSnapshot();

        foreach (var refreshable in refreshablesSnapshot)
        {
            if (refreshable.HasError)
            {
                encounteredError = true;
            }
            foreach (var (key, latestValue) in refreshable.HistoricValues)
            {
                if (key.ValueUsage == default || !valueUsages.Contains(key.ValueUsage.Value))
                {
                    continue;
                }
                result.TryAdd(key.ValueUsage.Value, new());
                result[key.ValueUsage.Value].Add(latestValue);
            }
        }

        hasErrors = encounteredError;
        return result;
    }

    public async Task RefreshValues()
    {
        _logger.LogTrace("{method}()", nameof(RefreshValues));
        using var scope = _serviceScopeFactory.CreateScope();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.DateTimeOffSetUtcNow();

        // snapshot to avoid modification during enumeration
        var refreshables = GetRefreshablesSnapshot();

        var tasks = refreshables
            .Where(r => !r.IsExecuting && (r.NextExecution == null || r.NextExecution <= now))
            .Select(r => r.RefreshValueAsync(CancellationToken.None));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task RecreateRefreshables(ConfigurationType? configurationType, params List<int> configurationIds)
    {
        _logger.LogTrace("{method}()", nameof(RecreateRefreshables));

        // 1) Request cancellation for any in-flight refresh
        var refreshablesSnapshot = GetRefreshablesSnapshot();

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
                _logger.LogWarning(ex, "One or more refresh tasks failed while cancelling during {method}.", nameof(RecreateRefreshables));
            }
        }

        // 3) Dispose old refreshables
        foreach (var refreshable in refreshablesToCancel)
        {
            try
            {
                await refreshable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing refreshable during {method}.", nameof(RecreateRefreshables));
            }
        }

        RemoveRefreshables(refreshablesToCancel);

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
        AddRefreshables(newRefreshables);
    }

    public List<IGenericValue<decimal>> GetSnapshot()
    {
        return new(GetRefreshablesSnapshot());
    }

    private List<IRefreshableValue<decimal>> GetRefreshablesSnapshot()
    {
        lock (_refreshablesLock)
        {
            return _refreshables.ToList();
        }
    }

    private void AddRefreshables(IEnumerable<IRefreshableValue<decimal>> refreshables)
    {
        lock (_refreshablesLock)
        {
            foreach (var refreshable in refreshables)
            {
                _refreshables.Add(refreshable);
            }
        }
    }

    private void RemoveRefreshables(List<IRefreshableValue<decimal>> refreshablesToCancel)
    {
        lock (_refreshablesLock)
        {
            foreach (var refreshable in refreshablesToCancel)
            {
                _refreshables.Remove(refreshable);
            }
        }
    }
}
