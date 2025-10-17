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
}

public sealed class DelegateRefreshableValue<T> : IRefreshableValue<T>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly Func<CancellationToken, Task<IReadOnlyDictionary<ValueUsage, T>>> _refresh;
    private readonly int _historicValueCapacity;

    public DelegateRefreshableValue(IDateTimeProvider dateTimeProvider, Func<CancellationToken, Task<IReadOnlyDictionary<ValueUsage, T>>> refresh,
        TimeSpan refreshInterval, int historicValueCapacity = 1)
    {
        _dateTimeProvider = dateTimeProvider;
        _refresh = refresh;
        _historicValueCapacity = historicValueCapacity;
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        RefreshInterval = refreshInterval;
        NextExecution = currentDate;
    }

    public IReadOnlyDictionary<ValueUsage, DtoHistoricValue<T>> HistoricValues
    {
        get
        {
            return _historicValues.ToDictionary().AsReadOnly();
        }
    }

    public bool IsExecuting { get; set; }

    private readonly Dictionary<ValueUsage, DtoHistoricValue<T>> _historicValues = new();
    public DateTimeOffset? NextExecution { get; }
    public TimeSpan RefreshInterval { get; set; }

    public async Task RefreshValueAsync(CancellationToken ct)
    {
        IsExecuting = true;
        try
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
        finally
        {
            IsExecuting = false;
        }
        
    }

    public ValueTask DisposeAsync()
    {
        //ToDo: Add dispose function to constructor and call it here
        //throw new NotImplementedException();
        return ValueTask.CompletedTask;
    }
}
