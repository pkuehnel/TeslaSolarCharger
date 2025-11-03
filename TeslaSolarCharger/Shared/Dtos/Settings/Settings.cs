using System.Collections.Concurrent;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Settings : ISettings
{
    public int? InverterPower { get; set; }
    public int? Overage { get; set; }
    public List<DtoCar> CarsToManage => Cars.Where(c => c.ShouldBeManaged == true).OrderBy(c => c.ChargingPriority).ToList();
    public int? HomeBatterySoc { get; set; }
    public int? LastLoggedHomeBatterySoc { get; set; }
    public int? HomeBatteryPower { get; set; }
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

    public DateTimeOffset? StartupTime { get; set; }

    public DtoProgress? ChargePricesUpdateProgress { get; set; }
    public int LastPvDemoCase { get; set; }

    public bool IsPreRelease { get; set; }
}

public enum NextSunEvent
{
    Unknown,
    Sunrise,
    Sunset,
}
