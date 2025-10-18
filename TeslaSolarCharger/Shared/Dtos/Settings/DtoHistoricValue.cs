namespace TeslaSolarCharger.Shared.Dtos.Settings;


public readonly record struct TimedValue<T>(DateTimeOffset Timestamp, T? Value);

public class DtoHistoricValue<T> : DtoTimeStampedValue<T>
{
    private readonly Queue<TimedValue<T?>> _history;

    /// <summary>Maximum number of history items to keep.</summary>
    private int Capacity { get; set; }

    /// <summary>Returns a snapshot of the history in chronological order (oldest -> newest).</summary>
    public IReadOnlyList<TimedValue<T?>> History
    {
        get
        {
            // Return a defensive copy so callers can't mutate the internal queue
            return new List<TimedValue<T?>>(_history);
        }
    }

    public DtoHistoricValue(DateTimeOffset timestamp, T? value, int capacity)
        : base(timestamp, value)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        Capacity = capacity;
        _history = new Queue<TimedValue<T?>>(capacity);

        // Record the initial value
        Enqueue(new TimedValue<T?>(timestamp, value));
    }

    /// <summary>
    /// Updates the value using the same rules as the base class and records the sample
    /// if the update is accepted (i.e., force == true or newTimestamp >= current Timestamp).
    /// Returns whether the value actually changed (same semantics as base.Update).
    /// </summary>
    public new bool Update(DateTimeOffset newTimestamp, T? newValue, bool force = false)
    {
        var previousTimestamp = Timestamp;

        // Use base behavior to determine acceptance and compute LastChanged
        var valueChanged = base.Update(newTimestamp, newValue, force);

        // If the update was accepted (either forced or timestamp >= previous), record it.
        if (force || newTimestamp >= previousTimestamp)
        {
            Enqueue(new TimedValue<T?>(newTimestamp, newValue));
        }

        return valueChanged;
    }

    /// <summary>
    /// Adjust history capacity at runtime. Trims oldest entries if shrinking.
    /// </summary>
    public void SetCapacity(int newCapacity)
    {
        if (newCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(newCapacity));
        Capacity = newCapacity;

        while (_history.Count > Capacity)
            _history.Dequeue();
    }

    private void Enqueue(TimedValue<T?> item)
    {
        if (_history.Count == Capacity)
            _history.Dequeue();
        _history.Enqueue(item);
    }
}
