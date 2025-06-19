using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Shared.Dtos.Contracts;

public interface ISettings
{
    int? InverterPower { get; set; }
    int? Overage { get; set; }
    int? HomeBatterySoc { get; set; }
    int? HomeBatteryPower { get; set; }
    bool ControlledACarAtLastCycle { get; set; }
    DateTimeOffset LastPvValueUpdate { get; set; }
    int? AverageHomeGridVoltage { get; set; }
    bool CrashedOnStartup { get; set; }
    string? StartupCrashMessage { get; set; }
    List<DtoCar> Cars { get; set; }
    List<DtoCar> CarsToManage { get; }
    bool RestartNeeded { get; set; }
    Dictionary<int, string> RawRestRequestResults { get; set; }
    Dictionary<int, string> RawRestValues { get; set; }
    Dictionary<int, decimal?> CalculatedRestValues { get; set; }
    bool IsStartupCompleted { get; set; }
    string? ChargePricesUpdateText { get; set; }
    DateTime StartupTime { get; set; }
    int LastPvDemoCase { get; set; }
    bool IsPreRelease { get; set; }

    /// <summary>
    /// Key is Id of the connector in database
    /// </summary>
    ConcurrentDictionary<int, DtoOcppConnectorState> OcppConnectorStates { get; set; }

    ConcurrentBag<DtoChargingSchedule> ChargingSchedules { get; set; }
    ConcurrentBag<DtoNotChargingWithExpectedPowerReason> GenericNotChargingWithExpectedPowerReasons { get; set; }
    ConcurrentDictionary<(int? carId, int? connectorId), List<DtoNotChargingWithExpectedPowerReason>> LoadPointSpecificNotChargingWithExpectedPowerReasons { get; set; }
    ConcurrentDictionary<int, (int? carId, DateTimeOffset combinationTimeStamp)> ManualSetLoadPointCarCombinations { get; set; }
}
