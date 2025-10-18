using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.ValueRefresh;

public interface IRefreshableValue<T> : IAsyncDisposable
{
    IReadOnlyDictionary<ValueUsage, DtoHistoricValue<T>> HistoricValues { get; }
    bool IsExecuting { get; set; }
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
    private readonly Dictionary<ValueUsage, DtoHistoricValue<T>> _historicValues = new();

    private readonly CancellationTokenSource _cts = new();

    public DelegateRefreshableValue(
        IDateTimeProvider dateTimeProvider,
        Func<CancellationToken, Task<IReadOnlyDictionary<ValueUsage, T>>> refresh,
        TimeSpan refreshInterval,
        int historicValueCapacity = 1)
    {
        _dateTimeProvider = dateTimeProvider;
        _refresh = refresh;
        _historicValueCapacity = historicValueCapacity;

        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        RefreshInterval = refreshInterval;
        NextExecution = currentDate;
    }

    public IReadOnlyDictionary<ValueUsage, DtoHistoricValue<T>> HistoricValues
        => _historicValues.ToDictionary().AsReadOnly();

    public bool IsExecuting { get; set; }

    public Task? RunningTask { get; private set; }

    public DateTimeOffset? NextExecution { get; }

    public TimeSpan RefreshInterval { get; set; }

    public async Task RefreshValueAsync(CancellationToken ct)
    {
        if (IsExecuting)
        {
            return;
        }

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
        }
    }

    private async Task DoRefreshAsync(CancellationToken ct)
    {
        var results = await _refresh(ct).ConfigureAwait(false);
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        foreach (var result in results)
        {
            if (_historicValues.TryGetValue(result.Key, out var value))
            {
                value.Update(currentDate, result.Value);
            }
            else
            {
                _historicValues.Add(result.Key, new(currentDate, result.Value, _historicValueCapacity));
            }
        }
    }

    public void Cancel()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already disposed; ignore
        }
    }

    public ValueTask DisposeAsync()
    {
        try { _cts.Cancel(); } catch { /* ignore */ }
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }
}
