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
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly Func<CancellationToken, Task<IReadOnlyDictionary<ValueUsage, T>>> _refresh;
    private readonly int _historicValueCapacity;
    private readonly ConcurrentDictionary<ValueUsage, DtoHistoricValue<T>> _historicValues = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _runGate = new(1, 1);

    public DelegateRefreshableValue(
        IDateTimeProvider dateTimeProvider,
        Func<CancellationToken, Task<IReadOnlyDictionary<ValueUsage, T>>> refresh,
        TimeSpan refreshInterval,
        int historicValueCapacity = 1)
    {
        _dateTimeProvider = dateTimeProvider;
        _refresh = refresh;
        _historicValueCapacity = historicValueCapacity;

        RefreshInterval = refreshInterval;
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
        RunningTask = DoRefreshAsync(token);

        try
        {
            await RunningTask.ConfigureAwait(false);
        }
        finally
        {
            IsExecuting = false;
            RunningTask = null;
            NextExecution = _dateTimeProvider.DateTimeOffSetUtcNow() + RefreshInterval;
            _runGate.Release();
        }
    }

    private async Task DoRefreshAsync(CancellationToken ct)
    {
        var results = await _refresh(ct).ConfigureAwait(false);
        var now = _dateTimeProvider.DateTimeOffSetUtcNow();

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
