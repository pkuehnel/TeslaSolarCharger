using TeslaSolarCharger.Server.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.ValueRefresh;

public class RefreshableValueHandlingService : IRefreshableValueHandlingService
{
    private readonly ILogger<RefreshableValueHandlingService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IRefreshableValue<decimal>> _refreshables = new();

    private const string RestPrefix = "rest__";

    public RefreshableValueHandlingService(ILogger<RefreshableValueHandlingService> logger, IServiceProvider serviceProvider)
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
        foreach (var refreshable in _refreshables)
        {
            foreach (var historicValue in refreshable.Value.HistoricValues)
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
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var refreshTasks = _refreshables.Values.Where(r => !r.IsExecuting
                                                           && (r.NextExecution == default || r.NextExecution <= currentDate))
            .Select(r => r.RefreshValueAsync(CancellationToken.None));
        await Task.WhenAll(refreshTasks).ConfigureAwait(false);
    }

    public async Task RecreateRefreshables()
    {
        _logger.LogTrace("{method}()", nameof(RecreateRefreshables));
        foreach (var refreshable in _refreshables.Values)
        {
            await refreshable.DisposeAsync().ConfigureAwait(false);
        }
        _refreshables.Clear();
        var valueUsages = new HashSet<ValueUsage>
        {
            ValueUsage.InverterPower,
            ValueUsage.GridPower,
            ValueUsage.HomeBatteryPower,
            ValueUsage.HomeBatterySoc,
        };
        var setupScope = _serviceProvider.CreateAsyncScope();
        await using var scope = setupScope.ConfigureAwait(false);
        var configurationWrapper = setupScope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
        var solarValueRefreshInterval = configurationWrapper.PvValueJobUpdateIntervall();
        var restValueConfigurationService = setupScope.ServiceProvider.GetRequiredService<IRestValueConfigurationService>();
        var restConfigurations = await restValueConfigurationService
            .GetFullRestValueConfigurationsByPredicate(c => c.RestValueResultConfigurations.Any(r => valueUsages.Contains(r.UsedFor))).ConfigureAwait(false);
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
                        // Fetch raw response for this configuration
                        var responseString = await restValueExecutionService
                            .GetResult(restConfiguration)
                            .ConfigureAwait(false);

                        // Get result mappings for this configuration
                        var resultConfigurations = await restValueConfigurationService
                            .GetResultConfigurationsByConfigurationId(restConfiguration.Id)
                            .ConfigureAwait(false);

                        // Sum values per ValueUsage for this configuration
                        var values = new Dictionary<ValueUsage, decimal>();
                        foreach (var resultConfig in resultConfigurations)
                        {
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
