namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoTimeStampedValue<T> : DtoValue<T>
{
    public DtoTimeStampedValue(DateTimeOffset timestamp, T? value)
    {
        Timestamp = timestamp;
        Value = value;
    }

    public DtoTimeStampedValue(T? value)
    {
        Timestamp = DateTimeOffset.UtcNow;
        Value = value;
    }

    public DateTimeOffset Timestamp { get; set; }
}
