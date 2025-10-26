using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Services.Services.ValueRefresh;

public interface IAutoRefreshingValue<T> : IGenericValue<T>
{
}


public class AutoRefreshingValue<T> : IAutoRefreshingValue<T>
{
    private readonly int _historicValueCapacity;

    private readonly ConcurrentDictionary<ValueKey, ConcurrentDictionary<int, DtoHistoricValue<T>>> _historicValues = new();

    public AutoRefreshingValue(int historicValueCapacity)
    {
        _historicValueCapacity = historicValueCapacity;
    }

    public IReadOnlyDictionary<ValueKey, ConcurrentDictionary<int, DtoHistoricValue<T>>> HistoricValues
    {
        get
        {
            return new ReadOnlyDictionary<ValueKey, ConcurrentDictionary<int, DtoHistoricValue<T>>>(_historicValues);
        }
    }


    public void UpdateValue(ValueKey valueKey, DateTimeOffset timestamp, T? value, int resultConfigId)
    {
        var historicValues = _historicValues.GetOrAdd(valueKey, new ConcurrentDictionary<int, DtoHistoricValue<T>>());
        var exists = historicValues.TryGetValue(resultConfigId, out var historicValue);
        if (exists)
        {
            historicValue?.Update(timestamp, value);
        }
        else
        {
            historicValue = new(timestamp, value, _historicValueCapacity);
            historicValues.TryAdd(resultConfigId, historicValue);

        }
    }
}
