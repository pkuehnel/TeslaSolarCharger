using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.ValueRefresh;

public interface IRefreshableValue<T> : IAsyncDisposable
{
    IReadOnlyDictionary<ValueUsage, DtoHistoricValue<T>> HistoricValues { get; }
    bool IsExecuting { get; }
    DateTimeOffset? NextExecution { get; }
    Task RefreshValueAsync(CancellationToken ct);

    Task? RunningTask { get; }
    void Cancel();
}

public sealed class DelegateRefreshableValue<T> : IRefreshableValue<T>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Func<CancellationToken, Task<IReadOnlyDictionary<ValueUsage, T>>> _refresh;
    private readonly int _historicValueCapacity;
    private readonly ConcurrentDictionary<ValueUsage, DtoHistoricValue<T>> _historicValues = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _runGate = new(1, 1);

    public DelegateRefreshableValue(
        IServiceScopeFactory serviceScopeFactory,
        Func<CancellationToken, Task<IReadOnlyDictionary<ValueUsage, T>>> refresh,
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

    public IReadOnlyDictionary<ValueUsage, DtoHistoricValue<T>> HistoricValues
    {
        get
        {
            return _historicValues.ToDictionary(kv => kv.Key, kv => kv.Value).AsReadOnly();
        }
    }

    public bool IsExecuting { get; private set; }
    public Task? RunningTask { get; private set; }
    public DateTimeOffset? NextExecution { get; private set; }
    private TimeSpan RefreshInterval { get; }

    public async Task RefreshValueAsync(CancellationToken ct)
    {
        // try enter without waiting — ensures no reentrancy
        if (!await _runGate.WaitAsync(0, ct).ConfigureAwait(false))
            return;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var token = linkedCts.Token;

        IsExecuting = true;
        using var scope = _serviceScopeFactory.CreateScope();
        RunningTask = DoRefreshAsync(scope, token);
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        try
        {
            await RunningTask.ConfigureAwait(false);
        }
        finally
        {
            RunningTask = null;
            NextExecution = dateTimeProvider.DateTimeOffSetUtcNow() + RefreshInterval;
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
            if (_historicValues.TryGetValue(result.Key, out var value))
            {
                value.Update(now, result.Value);
            }
            else
            {
                _historicValues.TryAdd(result.Key, new(now, result.Value, _historicValueCapacity));
            }
        }
        return now;
    }

    public void Cancel() { try { _cts.Cancel(); }
        catch
        {
            // ignored
        }
    }
    public ValueTask DisposeAsync() { try { _cts.Cancel(); }
        catch
        {
            // ignored
        }

        _cts.Dispose(); _runGate.Dispose(); return ValueTask.CompletedTask; }
}
