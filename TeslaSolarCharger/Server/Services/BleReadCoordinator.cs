using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BleReadCoordinator(ILogger<BleReadCoordinator> logger) : IBleReadCoordinator
{
    private readonly ConcurrentDictionary<int, byte> _carsBeingRead = new();

    public bool TryBeginRead(int carId)
    {
        var acquired = _carsBeingRead.TryAdd(carId, 0);
        if (!acquired)
        {
            logger.LogDebug("A BLE read for car {carId} is already in progress, skipping this one", carId);
        }
        return acquired;
    }

    public void EndRead(int carId)
    {
        _carsBeingRead.TryRemove(carId, out _);
    }
}
