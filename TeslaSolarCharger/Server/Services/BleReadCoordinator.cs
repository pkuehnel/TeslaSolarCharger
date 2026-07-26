using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BleReadCoordinator : IBleReadCoordinator
{
    //A car id is present while a BLE read for that car is running. The byte value is unused.
    private readonly ConcurrentDictionary<int, byte> _readsInProgress = new();

    public bool TryBeginRead(int carId)
    {
        return _readsInProgress.TryAdd(carId, 0);
    }

    public void EndRead(int carId)
    {
        _readsInProgress.TryRemove(carId, out _);
    }
}
