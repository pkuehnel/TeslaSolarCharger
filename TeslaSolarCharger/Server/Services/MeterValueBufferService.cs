using System.Collections.Concurrent;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueBufferService : IMeterValueBufferService
{
    private readonly ConcurrentQueue<MeterValue> _buffer = new ConcurrentQueue<MeterValue>();

    public void Add(MeterValue meterValue)
    {
        _buffer.Enqueue(meterValue);
    }

    /// <summary>
    /// Retrieves and removes all items from the buffer.
    /// </summary>
    public List<MeterValue> DrainAll()
    {
        var drainedItems = new List<MeterValue>();
        while (_buffer.TryDequeue(out var item))
        {
            drainedItems.Add(item);
        }
        return drainedItems;
    }
}
