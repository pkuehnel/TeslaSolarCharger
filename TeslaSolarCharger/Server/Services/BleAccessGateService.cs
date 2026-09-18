using System.Collections.Concurrent;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

/// <inheritdoc />
public class BleAccessGateService(ILogger<BleAccessGateService> logger, IDateTimeProvider dateTimeProvider)
    : IBleAccessGateService
{
    /// <summary>
    /// How long a car whose key was rejected is left alone. Long enough to keep the radio free for the cars that do
    /// work - the poll would otherwise connect to it every few seconds forever - and short enough that a key added
    /// somewhere else is noticed on its own. A key added through TSC never waits for it: the connection test that
    /// follows the pairing clears the rejection.
    /// </summary>
    internal static readonly TimeSpan KeyRejectionRetryInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// What the user is told while a car is paused. Says what the car said and what to do about it: nothing else
    /// will make BLE work for this car again.
    /// </summary>
    internal const string KeyNotPairedMessage =
        "The car rejected TSC's key, so it was not asked again. Add TSC's key to the car: open the car's settings, " +
        "test the Bluetooth connection and follow the steps to add the key.";

    private readonly ConcurrentDictionary<int, DateTimeOffset> _keyRejections = new();
    private readonly Dictionary<string, int> _pairingContainers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _pairingLock = new();

    public void RegisterCommandResult(int carId, DtoBleCommandResult result)
    {
        if (result.Outcome == BleCommandOutcome.KeyNotPaired)
        {
            if (!_keyRejections.ContainsKey(carId))
            {
                logger.LogWarning("Car {carId} rejected TSC's key. Pausing its BLE requests for {interval}", carId, KeyRejectionRetryInterval);
            }
            //Restarted on every rejection, so a car that keeps rejecting is asked once per interval and not more.
            _keyRejections[carId] = dateTimeProvider.DateTimeOffSetUtcNow();
            return;
        }
        if (!result.Success)
        {
            //A link failure says nothing about the key, so it may neither start nor end the pause.
            return;
        }
        if (_keyRejections.TryRemove(carId, out _))
        {
            logger.LogInformation("Car {carId} answered a BLE request again, resuming its BLE requests", carId);
        }
    }

    public bool IsKeyRejected(int carId)
    {
        if (!_keyRejections.TryGetValue(carId, out var rejectedAt))
        {
            return false;
        }
        if ((dateTimeProvider.DateTimeOffSetUtcNow() - rejectedAt) < KeyRejectionRetryInterval)
        {
            return true;
        }
        //The interval is over: let the car be asked again. It latches itself back if the key is still missing.
        _keyRejections.TryRemove(carId, out _);
        logger.LogDebug("Retry interval of car {carId} elapsed, asking it again", carId);
        return false;
    }

    public void ClearKeyRejection(int carId)
    {
        if (_keyRejections.TryRemove(carId, out _))
        {
            logger.LogDebug("Forgetting the key rejection of car {carId}", carId);
        }
    }

    public IDisposable BeginPairing(string? bleApiBaseUrl)
    {
        var key = PairingKey(bleApiBaseUrl);
        lock (_pairingLock)
        {
            _pairingContainers[key] = _pairingContainers.TryGetValue(key, out var count) ? count + 1 : 1;
        }
        logger.LogDebug("Pausing scheduled BLE reads on container {bleApiBaseUrl} while a key is added", bleApiBaseUrl);
        return new PairingScope(this, key);
    }

    public bool IsPairingInProgress(string? bleApiBaseUrl)
    {
        lock (_pairingLock)
        {
            return _pairingContainers.TryGetValue(PairingKey(bleApiBaseUrl), out var count) && (count > 0);
        }
    }

    //Counted rather than set and cleared: two pairings running at once on one container must not resume the reads
    //when the first of them returns.
    private void EndPairing(string key)
    {
        lock (_pairingLock)
        {
            if (!_pairingContainers.TryGetValue(key, out var count))
            {
                return;
            }
            if (count <= 1)
            {
                _pairingContainers.Remove(key);
            }
            else
            {
                _pairingContainers[key] = count - 1;
            }
        }
    }

    private static string PairingKey(string? bleApiBaseUrl) => bleApiBaseUrl?.Trim() ?? string.Empty;

    private sealed class PairingScope(BleAccessGateService owner, string key) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            owner.EndPairing(key);
        }
    }
}
