using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.ValueRefresh;

public class RefreshableValueHandlingService : IRefreshableValueHandlingService
{
    private readonly ILogger<RefreshableValueHandlingService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ConcurrentDictionary<string, IRefreshableValue<decimal>> _refreshables = new();

    private const string RestPrefix = "rest__";

    public RefreshableValueHandlingService(
        ILogger<RefreshableValueHandlingService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public IReadOnlyDictionary<ValueUsage, decimal> GetSolarValues()
    {
        _logger.LogTrace("{method}()", nameof(GetSolarValues));
        var result = new Dictionary<ValueUsage, decimal>();
        var valueUsages = new HashSet<ValueUsage>
        {
            ValueUsage.InverterPower,
            ValueUsage.GridPower,
            ValueUsage.HomeBatteryPower,
            ValueUsage.HomeBatterySoc,
        };

        foreach (var refreshable in _refreshables.Values)
        {
            foreach (var historicValue in refreshable.HistoricValues)
            {
                if (!valueUsages.Contains(historicValue.Key))
                {
                    continue;
                }
                var latestValue = historicValue.Value.Value;
                result.TryAdd(historicValue.Key, 0m);
                result[historicValue.Key] += latestValue;
            }
        }

        return result;
    }

    public async Task RefreshValues()
    {
        _logger.LogTrace("{method}()", nameof(RefreshValues));
        using var scope = _serviceScopeFactory.CreateScope();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var now = dateTimeProvider.DateTimeOffSetUtcNow();

        // snapshot to avoid modification during enumeration
        var refreshables = _refreshables.Values.ToArray();

        var tasks = refreshables
            .Where(r => !r.IsExecuting && (r.NextExecution == null || r.NextExecution <= now))
            .Select(r => r.RefreshValueAsync(CancellationToken.None));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task RecreateRefreshables()
    {
        _logger.LogTrace("{method}()", nameof(RecreateRefreshables));

        // 1) Request cancellation for any in-flight refresh
        foreach (var refreshable in _refreshables.Values)
        {
            if (refreshable.IsExecuting)
            {
                refreshable.Cancel();
            }
        }

        // 2) Await completion of any running tasks (best effort)
        var running = _refreshables.Values
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
        foreach (var refreshable in _refreshables.Values)
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

        _refreshables.Clear();

        // 4) Recreate refreshables as before
        var valueUsages = new HashSet<ValueUsage>
        {
            ValueUsage.InverterPower,
            ValueUsage.GridPower,
            ValueUsage.HomeBatteryPower,
            ValueUsage.HomeBatterySoc,
        };

        using var setupScope = _serviceScopeFactory.CreateScope();
        var configurationWrapper = setupScope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
        var solarValueRefreshInterval = configurationWrapper.PvValueJobUpdateIntervall();

        var setupRestValueConfigurationService = setupScope.ServiceProvider.GetRequiredService<IRestValueConfigurationService>();
        var restConfigurations = await setupRestValueConfigurationService
            .GetFullRestValueConfigurationsByPredicate(
                c => c.RestValueResultConfigurations.Any(r => valueUsages.Contains(r.UsedFor)))
            .ConfigureAwait(false);

        foreach (var restConfiguration in restConfigurations)
        {
            try
            {
                var key = $"{RestPrefix}{restConfiguration.Id}";

                var refreshable = new DelegateRefreshableValue<decimal>(
                    _serviceScopeFactory,
                    async ct =>
                    {
                        using var executionScope = _serviceScopeFactory.CreateScope();
                        var restValueExecutionService = executionScope.ServiceProvider.GetRequiredService<IRestValueExecutionService>();
                        var responseString = await restValueExecutionService
                            .GetResult(restConfiguration)
                            .ConfigureAwait(false);

                        var restValueConfigurationService = executionScope.ServiceProvider.GetRequiredService<IRestValueConfigurationService>();
                        var resultConfigurations = await restValueConfigurationService
                            .GetResultConfigurationsByConfigurationId(restConfiguration.Id)
                            .ConfigureAwait(false);

                        var values = new Dictionary<ValueUsage, decimal>();
                        foreach (var resultConfig in resultConfigurations)
                        {
                            ct.ThrowIfCancellationRequested();

                            var val = restValueExecutionService.GetValue(
                                responseString,
                                restConfiguration.NodePatternType,
                                resultConfig);

                            if (!values.TryGetValue(resultConfig.UsedFor, out var current))
                            {
                                values[resultConfig.UsedFor] = val;
                            }
                            else
                            {
                                values[resultConfig.UsedFor] = current + val;
                            }
                        }

                        return values.AsReadOnly();
                    },
                    solarValueRefreshInterval
                );

                _refreshables[key] = refreshable;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while creating refreshable for {restConfigurationId} with URL {url}",
                    restConfiguration.Id,
                    restConfiguration.Url);
            }
        }
    }
}
