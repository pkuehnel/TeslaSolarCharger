using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;

public interface IRefreshableValue<T> : IGenericValue<T>
{
    bool IsExecuting { get; }
    DateTimeOffset? NextExecution { get; }
    Task RefreshValueAsync(CancellationToken ct);

    Task? RunningTask { get; }
    
}

public sealed class DelegateRefreshableValue<T> : GenericValueBase<T>, IRefreshableValue<T>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Func<CancellationToken, Task<ConcurrentDictionary<ValueKey, T>>> _refresh;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private bool _isCanceled;

    public override SourceValueKey SourceValueKey { get; }

    public DelegateRefreshableValue(
        IServiceScopeFactory serviceScopeFactory,
        Func<CancellationToken, Task<ConcurrentDictionary<ValueKey, T>>> refresh,
        TimeSpan refreshInterval,
        int historicValueCapacity,
        SourceValueKey sourceValueKey) : base(serviceScopeFactory, historicValueCapacity)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _refresh = refresh;
        SourceValueKey = sourceValueKey;
        RefreshInterval = refreshInterval;
        using var scope = _serviceScopeFactory.CreateScope();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        NextExecution = dateTimeProvider.DateTimeOffSetUtcNow();
    }

    public bool IsExecuting { get; private set; }
    public Task? RunningTask { get; private set; }
    public DateTimeOffset? NextExecution { get; private set; }
    private TimeSpan RefreshInterval { get; }

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
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            SetErrorFromException(ex);
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

    public override void Cancel()
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
    public override ValueTask DisposeAsync()
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
