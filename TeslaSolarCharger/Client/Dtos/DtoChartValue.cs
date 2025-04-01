namespace TeslaSolarCharger.Client.Dtos;

public class DtoChartValue<TKey, TValue>
{
    public DtoChartValue(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }

    public TKey Key { get; set; }
    public TValue Value { get; set; }
}
