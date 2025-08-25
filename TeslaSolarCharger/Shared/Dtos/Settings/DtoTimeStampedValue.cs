namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoTimeStampedValue<T> : DtoValue<T>
{
    public DateTimeOffset Timestamp { get; private set; }
    public DateTimeOffset? LastChanged { get; private set; }

    // Hide the base setter completely:
    public new T? Value => base.Value;

    public DtoTimeStampedValue(DateTimeOffset timestamp, T? value)
    {
        Timestamp = timestamp;

        // Bypass the hidden setter here:
        base.Value = value;
    }

    public bool Update(DateTimeOffset newTimestamp, T? newValue)
    {
        if(newTimestamp <= Timestamp)
        {
            return false; // No update if the new timestamp is not greater than the current one.
        }
        Timestamp = newTimestamp;
        if (!EqualityComparer<T?>.Default.Equals(base.Value, newValue))
        {
            LastChanged = newTimestamp;
        }
        base.Value = newValue;
        return true;
    }
}
