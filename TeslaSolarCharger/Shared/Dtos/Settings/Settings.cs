using System.Collections.Concurrent;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Settings : ISettings
{
    public int? InverterPower { get; set; }
    public int? Overage { get; set; }
    public List<DtoCar> CarsToManage => Cars.Where(c => c.ShouldBeManaged == true).OrderBy(c => c.ChargingPriority).ToList();
    public int? HomeBatterySoc { get; set; }
    public int? LastLoggedHomeBatterySoc { get; set; }
    /// <summary>
    /// Learned metered energy in Wh that charges the home battery from 0 to 100 %, null while charging and discharging are
    /// not both reliably learned.
    /// </summary>
    public int? LearnedHomeBatteryChargingEnergyWh { get; set; }
    /// <summary>
    /// Learned metered energy in Wh the home battery delivers from 100 to 0 %, null while charging and discharging are
    /// not both reliably learned.
    /// </summary>
    public int? LearnedHomeBatteryDischargingEnergyWh { get; set; }
    /// <summary>Reliable usable energy in Wh learned from past charges, by car id. Cars without a reliable value are missing.</summary>
    public ConcurrentDictionary<int, int> LearnedCarUsableEnergiesWh { get; set; } = new();
    public int? HomeBatteryPower { get; set; }
    public bool ControlledACarAtLastCycle { get; set; }
    public DateTimeOffset LastPvValueUpdate { get; set; }
    public int? AverageHomeGridVoltage { get; set; }

    public bool CrashedOnStartup { get; set; }
    public string? StartupCrashMessage { get; set; }
    public bool RestartNeeded { get; set; }

    public NextSunEvent NextSunEvent { get; set; } = NextSunEvent.Unknown;

    public bool IsHomeBatteryDischargingActive { get; set; } = true;

    public DtoHomeBatterySocTarget? HomeBatteryHoldTarget { get; set; }
    public DtoHomeBatterySocTarget? HomeBatteryChargeTarget { get; set; }
    public ConcurrentBag<DtoHomeBatteryScheduleWindow> HomeBatteryScheduleWindows { get; set; } = new();

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
    public ConcurrentDictionary<int, string> RawRestRequestResults { get; set; } = new();
    public ConcurrentDictionary<int, string> RawRestValues { get; set; } = new();
    public Dictionary<int, decimal?> CalculatedRestValues { get; set; } = new();

    public bool IsStartupCompleted { get; set; }

    public DateTimeOffset? StartupTime { get; set; }

    public DtoProgress? ChargePricesUpdateProgress { get; set; }
    public ConcurrentDictionary<int, DtoCarDeletionProgress> CarDeletionProgresses { get; set; } = new();
    public ConcurrentDictionary<int, DtoChargingStationDeletionProgress> ChargingStationDeletionProgresses { get; set; } = new();
    public int LastPvDemoCase { get; set; }

    public bool IsPreRelease { get; set; }
}

public enum NextSunEvent
{
    Unknown,
    Sunrise,
    Sunset,
}
