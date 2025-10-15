using System;
using System.Collections.Concurrent;
using System.Linq;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Settings : ISettings
{
    public ConcurrentDictionary<SolarDeviceKey, SolarDeviceState> SolarDevices { get; } = new();

    public double? GetAverageValue(ValueUsage usage, TimeSpan window, DateTimeOffset? now = null)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        var effectiveNow = now ?? DateTimeOffset.UtcNow;
        double sum = 0;
        var count = 0;

        foreach (var device in SolarDevices.Values)
        {
            foreach (var sample in device.GetHistorySnapshot(usage, window, effectiveNow))
            {
                if (!sample.Value.HasValue)
                {
                    continue;
                }

                sum += sample.Value.Value;
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        return sum / count;
    }

    public int? InverterPower => SumLatest(ValueUsage.InverterPower, clampToZero: true);
    public int? Overage => SumLatest(ValueUsage.GridPower);
    public List<DtoCar> CarsToManage => Cars.Where(c => c.ShouldBeManaged == true).OrderBy(c => c.ChargingPriority).ToList();
    public int? HomeBatterySoc => SumLatest(ValueUsage.HomeBatterySoc);
    public int? LastLoggedHomeBatterySoc { get; set; }
    public int? HomeBatteryPower => SumLatest(ValueUsage.HomeBatteryPower);
    public bool ControlledACarAtLastCycle { get; set; }
    public DateTimeOffset LastPvValueUpdate { get; set; }
    public int? AverageHomeGridVoltage { get; set; }

    public bool CrashedOnStartup { get; set; }
    public string? StartupCrashMessage { get; set; }
    public bool RestartNeeded { get; set; }

    public NextSunEvent NextSunEvent { get; set; } = NextSunEvent.Unknown;

    public bool IsHomeBatteryDischargingActive { get; set; } = true;

    public HashSet<DtoLoadpointCombination> LatestLoadPointCombinations { get; set; } = new();

    public List<DtoCar> Cars { get; set; } = new();
    /// <summary>
    /// Key is Id of the connector in database
    /// </summary>
    public ConcurrentDictionary<int, DtoOcppConnectorState> OcppConnectorStates { get; set; } = new();

    public ConcurrentBag<NotChargingWithExpectedPowerReasonTemplate> GenericNotChargingWithExpectedPowerReasons { get; set; } = new();
    public ConcurrentDictionary<(int? carId, int? connectorId), List<NotChargingWithExpectedPowerReasonTemplate>> LoadPointSpecificNotChargingWithExpectedPowerReasons
    { get; set; } = new();

    public ConcurrentDictionary<int, (int? carId, DateTimeOffset combinationTimeStamp)> ManualSetLoadPointCarCombinations { get; set; } = new();

    public ConcurrentDictionary<int, DateTimeOffset> CarsWithNonZeroMeterValueAddedLastCycle { get; set; } = new();
    public ConcurrentDictionary<int, DateTimeOffset> ChargingConnectorsWithNonZeroMeterValueAddedLastCycle { get; set; } = new();

    public ConcurrentBag<DtoChargingSchedule> ChargingSchedules { get; set; } = new();
    public Dictionary<int, string> RawRestRequestResults { get; set; } = new();
    public Dictionary<int, string> RawRestValues { get; set; } = new();
    public Dictionary<int, decimal?> CalculatedRestValues { get; set; } = new();

    public bool IsStartupCompleted { get; set; }

    public DateTime StartupTime { get; set; }

    public DtoProgress? ChargePricesUpdateProgress { get; set; }
    public int LastPvDemoCase { get; set; }

    public bool IsPreRelease { get; set; }

    private int? SumLatest(ValueUsage usage, bool clampToZero = false)
    {
        long sum = 0;
        var hasValue = false;

        foreach (var device in SolarDevices.Values)
        {
            var currentValue = device.GetCurrentValue(usage);
            if (!currentValue.HasValue)
            {
                continue;
            }

            sum += currentValue.Value;
            hasValue = true;
        }

        if (!hasValue)
        {
            return null;
        }

        if (clampToZero && sum < 0)
        {
            sum = 0;
        }

        if (sum > int.MaxValue)
        {
            return int.MaxValue;
        }

        if (sum < int.MinValue)
        {
            return int.MinValue;
        }

        return (int)sum;
    }
}

public enum NextSunEvent
{
    Unknown,
    Sunrise,
    Sunset,
}
