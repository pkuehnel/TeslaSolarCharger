using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.ValueRefresh;

public class RefreshableValueHandlingService : IRefreshableValueHandlingService
{
    private readonly ILogger<RefreshableValueHandlingService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HashSet<IRefreshableValue<decimal>> _refreshables = new();


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

        foreach (var refreshable in _refreshables)
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
                foreach (var historicValue in latestValue.Values)
                {
                    result[key.ValueUsage.Value].Add(historicValue);
                }
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
        var refreshables = _refreshables.ToArray();

        var tasks = refreshables
            .Where(r => !r.IsExecuting && (r.NextExecution == null || r.NextExecution <= now))
            .Select(r => r.RefreshValueAsync(CancellationToken.None));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task RecreateRefreshables()
    {
        _logger.LogTrace("{method}()", nameof(RecreateRefreshables));

        // 1) Request cancellation for any in-flight refresh
        foreach (var refreshable in _refreshables)
        {
            if (refreshable.IsExecuting)
            {
                refreshable.Cancel();
            }
        }

        // 2) Await completion of any running tasks (best effort)
        var running = _refreshables
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
        foreach (var refreshable in _refreshables)
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

                        var values = new Dictionary<ValueKey, ConcurrentDictionary<int, decimal>>();
                        foreach (var resultConfig in resultConfigurations)
                        {
                            ct.ThrowIfCancellationRequested();
                            var valueKey = new ValueKey(restConfiguration.Id, ConfigurationType.RestSolarValue, resultConfig.UsedFor, null);

                            var val = restValueExecutionService.GetValue(
                                responseString,
                                restConfiguration.NodePatternType,
                                resultConfig);

                            if (!values.TryGetValue(valueKey, out var current))
                            {
                                current = new();
                                values[valueKey] = current;
                            }
                            current.TryAdd(resultConfig.Id, val);
                        }

                        return values.ToDictionary().AsReadOnly();
                    },
                    solarValueRefreshInterval
                );

                _refreshables.Add(refreshable);
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

        var setupModbusValueConfigurationService = setupScope.ServiceProvider
            .GetRequiredService<IModbusValueConfigurationService>();
        var modbusConfigurations = await setupModbusValueConfigurationService
            .GetModbusConfigurationByPredicate(
                c => c.ModbusResultConfigurations.Any(r => valueUsages.Contains(r.UsedFor)))
            .ConfigureAwait(false);

        foreach (var modbusConfiguration in modbusConfigurations)
        {
            try
            {
                var configuration = modbusConfiguration;
                var refreshable = new DelegateRefreshableValue<decimal>(
                    _serviceScopeFactory,
                    async ct =>
                    {
                        using var executionScope = _serviceScopeFactory.CreateScope();
                        var modbusValueConfigurationService = executionScope.ServiceProvider
                            .GetRequiredService<IModbusValueConfigurationService>();
                        var modbusValueExecutionService = executionScope.ServiceProvider
                            .GetRequiredService<IModbusValueExecutionService>();

                        var resultConfigurations = await modbusValueConfigurationService
                            .GetResultConfigurationsByValueConfigurationId(configuration.Id)
                            .ConfigureAwait(false);

                        var values = new Dictionary<ValueKey, ConcurrentDictionary<int, decimal>>();
                        foreach (var resultConfiguration in resultConfigurations)
                        {
                            ct.ThrowIfCancellationRequested();
                            var valueKey = new ValueKey(configuration.Id, ConfigurationType.ModbusSolarValue, resultConfiguration.UsedFor, null);
                            try
                            {
                                var byteArray = await modbusValueExecutionService
                                    .GetResult(configuration, resultConfiguration, false)
                                    .ConfigureAwait(false);
                                var value = await modbusValueExecutionService
                                    .GetValue(byteArray, resultConfiguration)
                                    .ConfigureAwait(false);

                                if (!values.TryGetValue(valueKey, out var current))
                                {
                                    current = new();
                                    values[valueKey] = current;
                                }
                                current.TryAdd(resultConfiguration.Id, value);

                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    ex,
                                    "Error while refreshing modbus value for configuration {configurationId} result {resultId}",
                                    configuration.Id,
                                    resultConfiguration.Id);
                                throw;
                            }
                        }

                        return values.AsReadOnly();
                    },
                    solarValueRefreshInterval
                );

                _refreshables.Add(refreshable);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while creating refreshable for modbus configuration {configurationId} ({host}:{port})",
                    modbusConfiguration.Id,
                    modbusConfiguration.Host,
                    modbusConfiguration.Port);
            }
        }
    }
}
