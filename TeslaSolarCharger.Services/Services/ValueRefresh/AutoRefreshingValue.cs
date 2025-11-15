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

    private readonly ConcurrentDictionary<ValueKey, DtoHistoricValue<T>> _historicValues = new();

    public SourceValueKey SourceValueKey { get; }

    public AutoRefreshingValue(SourceValueKey sourceValueKey, int historicValueCapacity)
    {
        _historicValueCapacity = historicValueCapacity;
        SourceValueKey = sourceValueKey;
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
}
