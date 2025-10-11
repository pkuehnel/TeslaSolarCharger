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

    /// <summary>
    /// Update saved value if the new timestamp is greater than or equal to the current one.
    /// </summary>
    /// <param name="newTimestamp">Timestamp of new value</param>
    /// <param name="newValue">new value</param>
    /// <param name="force">overwrite value even if current value has a newer timestamp than newValue</param>
    /// <returns>New value is different from old one</returns>
    public bool Update(DateTimeOffset newTimestamp, T? newValue, bool force = false)
    {
        if (!force && newTimestamp < Timestamp)
        {
            return false; // No update if the new timestamp is not greater than the current one.
        }
        Timestamp = newTimestamp;
        var valueChanged = false;
        if (!EqualityComparer<T?>.Default.Equals(base.Value, newValue))
        {
            LastChanged = newTimestamp;
            valueChanged = true;
        }
        base.Value = newValue;
        return valueChanged;
    }
}
