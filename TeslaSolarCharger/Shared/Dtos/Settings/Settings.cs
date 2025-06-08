using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos.Contracts;

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
