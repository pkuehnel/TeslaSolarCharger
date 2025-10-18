using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.ValueRefresh;

public class RefreshableValueHandlingService : IRefreshableValueHandlingService
{
    private readonly ILogger<RefreshableValueHandlingService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, IRefreshableValue<decimal>> _refreshables = new();

    private const string RestPrefix = "rest__";

    public RefreshableValueHandlingService(
        ILogger<RefreshableValueHandlingService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
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
        var dateTimeProvider = _serviceProvider.GetRequiredService<IDateTimeProvider>();
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

        using var setupScope = _serviceProvider.CreateScope();
        var configurationWrapper = setupScope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
        var solarValueRefreshInterval = configurationWrapper.PvValueJobUpdateIntervall();

        var restValueConfigurationService = setupScope.ServiceProvider.GetRequiredService<IRestValueConfigurationService>();
        var restConfigurations = await restValueConfigurationService
            .GetFullRestValueConfigurationsByPredicate(
                c => c.RestValueResultConfigurations.Any(r => valueUsages.Contains(r.UsedFor)))
            .ConfigureAwait(false);

        var dateTimeProvider = setupScope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var restValueExecutionService = setupScope.ServiceProvider.GetRequiredService<IRestValueExecutionService>();

        foreach (var restConfiguration in restConfigurations)
        {
            try
            {
                var key = $"{RestPrefix}{restConfiguration.Id}";

                var refreshable = new DelegateRefreshableValue<decimal>(
                    dateTimeProvider,
                    async ct =>
                    {
                        var responseString = await restValueExecutionService
                            .GetResult(restConfiguration)
                            .ConfigureAwait(false);

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
