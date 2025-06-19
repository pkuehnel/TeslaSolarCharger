using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Settings : ISettings
{
    public int? InverterPower { get; set; }
    public int? Overage { get; set; }
    public List<DtoCar> CarsToManage => Cars.Where(c => c.ShouldBeManaged == true).OrderBy(c => c.ChargingPriority).ToList();
    public int? HomeBatterySoc { get; set; }
    public int? HomeBatteryPower { get; set; }
    public bool ControlledACarAtLastCycle { get; set; }
    public DateTimeOffset LastPvValueUpdate { get; set; }
    public int? AverageHomeGridVoltage { get; set; }

    public bool CrashedOnStartup { get; set; }
    public string? StartupCrashMessage { get; set; }
    public bool RestartNeeded { get; set; }

    public List<DtoCar> Cars { get; set; } = new();
    /// <summary>
    /// Key is Id of the connector in database
    /// </summary>
    public ConcurrentDictionary<int, DtoOcppConnectorState> OcppConnectorStates { get; set; } = new();

    public ConcurrentBag<DtoNotChargingWithExpectedPowerReason> GenericNotChargingWithExpectedPowerReasons { get; set; } = new();
    public ConcurrentDictionary<(int? carId, int? connectorId), List<DtoNotChargingWithExpectedPowerReason>> LoadPointSpecificNotChargingWithExpectedPowerReasons
    { get; set; } = new();

    public ConcurrentDictionary<int, List<string>> CarsChargingReasons { get; set; } = new();
    public ConcurrentDictionary<int, List<string>> CarsNotChargingReasons { get; set; } = new();
    public ConcurrentDictionary<int, List<string>> ChargingConnectorChargingReasons { get; set; } = new();
    public ConcurrentDictionary<int, List<string>> ChargingConnectorNotChargingReasons { get; set; } = new();
    public ConcurrentDictionary<int, (int? carId, DateTimeOffset combinationTimeStamp)> ManualSetLoadPointCarCombinations { get; set; } = new();

    public ConcurrentBag<DtoChargingSchedule> ChargingSchedules { get; set; } = new();
    public Dictionary<int, string> RawRestRequestResults { get; set; } = new();
    public Dictionary<int, string> RawRestValues { get; set; } = new();
    public Dictionary<int, decimal?> CalculatedRestValues { get; set; } = new();

    public bool IsStartupCompleted { get; set; }

    public DateTime StartupTime { get; set; }

    public string? ChargePricesUpdateText { get; set; }
    public int LastPvDemoCase { get; set; }

    public bool IsPreRelease { get; set; }
}
