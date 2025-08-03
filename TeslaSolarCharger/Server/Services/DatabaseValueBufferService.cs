using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class DatabaseValueBufferService : IDatabaseValueBufferService
{
    private readonly ConcurrentDictionary<Type, object> _buffers = new ConcurrentDictionary<Type, object>();

    public void Add<T>(T item)
    {
        var buffer = GetOrCreateBuffer<T>();
        buffer.Enqueue(item);
    }

    /// <summary>
    /// Retrieves and removes items of type T that match the predicate.
    /// </summary>
    public List<T> DrainAll<T>()
    {
        var buffer = GetOrCreateBuffer<T>();
        var drainedItems = new List<T>();

        while (buffer.TryDequeue(out var item))
        {
            drainedItems.Add(item);
        }

        return drainedItems;
    }

    private ConcurrentQueue<T> GetOrCreateBuffer<T>()
    {
        return (ConcurrentQueue<T>)_buffers.GetOrAdd(typeof(T), _ => new ConcurrentQueue<T>());
    }
}
