using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Services.Services.ValueRefresh;

public interface IRefreshableValue<T> : IGenericValue<T>, IAsyncDisposable
{
    bool IsExecuting { get; }
    DateTimeOffset? NextExecution { get; }
    bool HasError { get; }
    Task RefreshValueAsync(CancellationToken ct);

    Task? RunningTask { get; }
    void Cancel();
}

public sealed class DelegateRefreshableValue<T> : IRefreshableValue<T>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Func<CancellationToken, Task<IReadOnlyDictionary<ValueKey, ConcurrentDictionary<int, T>>>> _refresh;
    private readonly int _historicValueCapacity;
    private readonly ConcurrentDictionary<ValueKey, ConcurrentDictionary<int, DtoHistoricValue<T>>> _historicValues = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private Exception? _lastError;

    public DelegateRefreshableValue(
        IServiceScopeFactory serviceScopeFactory,
        Func<CancellationToken, Task<IReadOnlyDictionary<ValueKey, ConcurrentDictionary<int, T>>>> refresh,
        TimeSpan refreshInterval,
        int historicValueCapacity = 1)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _refresh = refresh;
        _historicValueCapacity = historicValueCapacity;
        RefreshInterval = refreshInterval;
        using var scope = _serviceScopeFactory.CreateScope();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        NextExecution = dateTimeProvider.DateTimeOffSetUtcNow();
    }

    public IReadOnlyDictionary<ValueKey, ConcurrentDictionary<int, DtoHistoricValue<T>>> HistoricValues
    {
        get
        {
            return _historicValues.AsReadOnly();
        }
    }

    public void UpdateValue(ValueKey valueKey, DateTimeOffset timestamp, T? value, int configId = 0)
    {
        if (!_historicValues.TryGetValue(valueKey, out var valueDictionary))
        {
            valueDictionary = new();
            _historicValues.TryAdd(valueKey, valueDictionary);
        }
        if (valueDictionary.TryGetValue(configId, out var historicValue))
        {
            historicValue.Update(timestamp, value);
        }
        else
        {
            historicValue = new DtoHistoricValue<T>(timestamp, value, _historicValueCapacity);
            valueDictionary.TryAdd(configId, historicValue);
        }
    }

    public bool IsExecuting { get; private set; }
    public Task? RunningTask { get; private set; }
    public DateTimeOffset? NextExecution { get; private set; }
    private TimeSpan RefreshInterval { get; }
    public bool HasError => _lastError is not null;

    public async Task RefreshValueAsync(CancellationToken ct)
    {
        // try enter without waiting — ensures no reentrancy
        if (!await _runGate.WaitAsync(0, ct).ConfigureAwait(false))
            return;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var token = linkedCts.Token;

        IsExecuting = true;
        using var scope = _serviceScopeFactory.CreateScope();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        RunningTask = DoRefreshAsync(scope, token);
        try
        {
            await RunningTask.ConfigureAwait(false);
            _lastError = null;
        }
        catch (Exception ex)
        {
            _lastError = ex;
            throw;
        }
        finally
        {
            RunningTask = null;
            NextExecution = currentDate + RefreshInterval;
            IsExecuting = false;
            _runGate.Release();
        }
    }

    private async Task<DateTimeOffset> DoRefreshAsync(IServiceScope scope, CancellationToken ct)
    {
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var results = await _refresh(ct).ConfigureAwait(false);
        var now = dateTimeProvider.DateTimeOffSetUtcNow();

        foreach (var result in results)
        {
            foreach (var resultValue in result.Value)
            {
                UpdateValue(result.Key, now, resultValue.Value);
            }
        }
        return now;
    }

    public void Cancel()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignored
        }
    }
    public ValueTask DisposeAsync()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignored
        }

        _cts.Dispose();
        _runGate.Dispose();
        return ValueTask.CompletedTask;
    }
}
