using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class BlePresenceStateService(ILogger<BlePresenceStateService> logger) : IBlePresenceStateService
{
    //A single out of range result can be a transient BLE stack failure while the car is at home. Such failures can
    //occur multiple times in a row, so only this many consecutive out of range results confirm the car as away.
    internal const int ConsecutiveOutOfRangeResultsToConfirmAway = 5;

    private readonly ConcurrentDictionary<int, int> _consecutiveOutOfRangeResults = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRadioEvidence = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterSuccessfulRead(int carId)
    {
        if (_consecutiveOutOfRangeResults.TryRemove(carId, out var previousCount) && previousCount > 0)
        {
            logger.LogDebug("Car {carId} answered via BLE after {count} out of range results, car is in range", carId, previousCount);
        }
    }

    public BleAwayConfirmation RegisterOutOfRange(int carId)
    {
        //Cap the counter right above the threshold so an away car parked elsewhere for days cannot overflow the value.
        var newCount = _consecutiveOutOfRangeResults.AddOrUpdate(carId, 1,
            (_, count) => Math.Min(count + 1, ConsecutiveOutOfRangeResultsToConfirmAway + 1));
        if (newCount < ConsecutiveOutOfRangeResultsToConfirmAway)
        {
            logger.LogDebug("Car {carId} out of BLE range result {count}/{threshold}, keeping last known state", carId,
                newCount, ConsecutiveOutOfRangeResultsToConfirmAway);
            return BleAwayConfirmation.NotConfirmed;
        }
        return newCount == ConsecutiveOutOfRangeResultsToConfirmAway
            ? BleAwayConfirmation.JustConfirmed
            : BleAwayConfirmation.AlreadyConfirmed;
    }

    public bool IsPresenceUncertain(int carId)
    {
        return _consecutiveOutOfRangeResults.TryGetValue(carId, out var count)
               && count > 0
               && count < ConsecutiveOutOfRangeResultsToConfirmAway;
    }

    public void Reset(int carId)
    {
        if (_consecutiveOutOfRangeResults.TryRemove(carId, out _))
        {
            logger.LogDebug("Reset BLE presence state of car {carId}", carId);
        }
    }

    public void RetainOnly(IReadOnlyCollection<int> carIds)
    {
        foreach (var trackedCarId in _consecutiveOutOfRangeResults.Keys)
        {
            if (!carIds.Contains(trackedCarId))
            {
                Reset(trackedCarId);
            }
        }
    }

    public TimeSpan RegisterScanEvidence(string containerKey, bool heardAnything, DateTimeOffset timestamp)
    {
        if (heardAnything)
        {
            _lastRadioEvidence[containerKey] = timestamp;
            return TimeSpan.Zero;
        }
        //Seed on the first ever registration so a freshly started server never reports a silence longer than its own
        //observation window.
        var lastEvidence = _lastRadioEvidence.GetOrAdd(containerKey, timestamp);
        return timestamp - lastEvidence;
    }
}
