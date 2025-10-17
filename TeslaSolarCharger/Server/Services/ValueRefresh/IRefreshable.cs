using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.ValueRefresh;

public interface IRefreshable<T>
{
    DtoHistoricValue<T> HistoricValue { get; }
    TimeSpan RefreshInterval { get; set; }
    Task RefreshValueAsync(CancellationToken ct);
}

public sealed class DelegateRefreshable<T> : IRefreshable<T>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly Func<CancellationToken, Task<T>> _refresh;

    public DelegateRefreshable(IDateTimeProvider dateTimeProvider, Func<CancellationToken, Task<T>> refresh, TimeSpan refreshInterval, int historicValueCapacity = 1)
    {
        _dateTimeProvider = dateTimeProvider;
        _refresh = refresh;
        HistoricValue = new DtoHistoricValue<T>(dateTimeProvider.DateTimeOffSetUtcNow(), default, historicValueCapacity);
        RefreshInterval = refreshInterval;
    }

    public DtoHistoricValue<T> HistoricValue { get; }
    public TimeSpan RefreshInterval { get; set; }

    public async Task RefreshValueAsync(CancellationToken ct)
    {
        HistoricValue.Update(_dateTimeProvider.DateTimeOffSetUtcNow(), await _refresh(ct).ConfigureAwait(false));
    }
}
