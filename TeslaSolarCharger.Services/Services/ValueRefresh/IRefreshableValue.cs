using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
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
    private readonly Func<CancellationToken, Task<ConcurrentDictionary<ValueKey, T>>> _refresh;
    private readonly int _historicValueCapacity;
    private readonly ConcurrentDictionary<ValueKey, DtoHistoricValue<T>> _historicValues = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private Exception? _lastError;
    private bool _isCanceled;

    public SourceValueKey SourceValueKey { get; }

    public DelegateRefreshableValue(
        IServiceScopeFactory serviceScopeFactory,
        Func<CancellationToken, Task<ConcurrentDictionary<ValueKey, T>>> refresh,
        TimeSpan refreshInterval,
        int historicValueCapacity,
        SourceValueKey sourceValueKey)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _refresh = refresh;
        _historicValueCapacity = historicValueCapacity;
        SourceValueKey = sourceValueKey;
        RefreshInterval = refreshInterval;
        using var scope = _serviceScopeFactory.CreateScope();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        NextExecution = dateTimeProvider.DateTimeOffSetUtcNow();
    }

    public IReadOnlyDictionary<ValueKey, DtoHistoricValue<T>> HistoricValues
    {
        get
        {
            return new ReadOnlyDictionary<ValueKey, DtoHistoricValue<T>>(_historicValues);
        }
    }

    public void UpdateValue(ValueKey valueKey, DateTimeOffset timestamp, T? value)
    {
        var exists = _historicValues.TryGetValue(valueKey, out var historicValue);
        if (exists)
        {
            historicValue?.Update(timestamp, value);
        }
        else
        {
            historicValue = new(timestamp, value, _historicValueCapacity);
            _historicValues.TryAdd(valueKey, historicValue);
        }
    }

    public bool IsExecuting { get; private set; }
    public Task? RunningTask { get; private set; }
    public DateTimeOffset? NextExecution { get; private set; }
    private TimeSpan RefreshInterval { get; }
    public bool HasError => _lastError is not null;

    public async Task RefreshValueAsync(CancellationToken ct)
    {
        if(_isCanceled)
            return;
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
            UpdateValue(result.Key, now, result.Value);
        }
        return now;
    }

    public void Cancel()
    {
        try
        {
            _isCanceled = true;
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
