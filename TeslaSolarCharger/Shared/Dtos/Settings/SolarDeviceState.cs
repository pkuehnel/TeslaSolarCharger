using System;
using System.Collections.Generic;
using System.Linq;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class SolarDeviceState
{
    private readonly object _gate = new();
    private readonly Dictionary<ValueUsage, DtoHistoricValue<int?>> _histories = new();

    public SolarDeviceState(SolarDeviceKey key, string name, TimeSpan refreshInterval)
    {
        DeviceKey = key;
        Name = name;
        UpdateRefreshInterval(refreshInterval);
    }

    public SolarDeviceKey DeviceKey { get; }

    public string Name { get; }

    public TimeSpan RefreshInterval { get; private set; }

    public void UpdateRefreshInterval(TimeSpan refreshInterval)
    {
        if (refreshInterval <= TimeSpan.Zero)
        {
            refreshInterval = TimeSpan.FromSeconds(1);
        }

        RefreshInterval = refreshInterval;
    }

    public void UpdateHistory(ValueUsage usage, DateTimeOffset timestamp, int? value, int minimumCapacity)
    {
        lock (_gate)
        {
            var capacity = Math.Max(1, minimumCapacity);
            if (!_histories.TryGetValue(usage, out var history))
            {
                history = new DtoHistoricValue<int?>(timestamp, value, capacity);
                _histories[usage] = history;
                return;
            }

            history.SetCapacity(capacity);
            history.Update(timestamp, value);
        }
    }

    public int? GetCurrentValue(ValueUsage usage)
    {
        lock (_gate)
        {
            if (_histories.TryGetValue(usage, out var history))
            {
                return history.Value;
            }

            return null;
        }
    }

    public DateTimeOffset? GetCurrentTimestamp(ValueUsage usage)
    {
        lock (_gate)
        {
            if (_histories.TryGetValue(usage, out var history))
            {
                return history.Timestamp;
            }

            return null;
        }
    }

    public IReadOnlyList<TimedValue<int?>> GetHistorySnapshot(ValueUsage usage)
    {
        lock (_gate)
        {
            if (_histories.TryGetValue(usage, out var history))
            {
                return history.History.ToList();
            }

            return Array.Empty<TimedValue<int?>>();
        }
    }

    public IReadOnlyList<TimedValue<int?>> GetHistorySnapshot(ValueUsage usage, TimeSpan window, DateTimeOffset? now = null)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        var effectiveNow = now ?? DateTimeOffset.UtcNow;
        var cutoff = effectiveNow - window;

        lock (_gate)
        {
            if (!_histories.TryGetValue(usage, out var history))
            {
                return Array.Empty<TimedValue<int?>>();
            }

            return history.History
                .Where(sample => sample.Timestamp >= cutoff)
                .ToList();
        }
    }

    public double? GetAverageValue(ValueUsage usage, TimeSpan window, DateTimeOffset? now = null)
    {
        var snapshot = GetHistorySnapshot(usage, window, now);
        if (snapshot.Count == 0)
        {
            return null;
        }

        double sum = 0;
        var count = 0;

        foreach (var sample in snapshot)
        {
            if (!sample.Value.HasValue)
            {
                continue;
            }

            sum += sample.Value.Value;
            count++;
        }

        return count == 0 ? null : sum / count;
    }
}
