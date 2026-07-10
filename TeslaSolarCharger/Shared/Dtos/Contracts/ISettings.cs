using System.Collections.Concurrent;
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
    DtoProgress? ChargePricesUpdateProgress { get; set; }
    //Keyed by the id of the car currently being deleted, so concurrent deletions (e.g. multiple browser tabs)
    //do not clobber each other's progress. An entry exists only while that car's deletion is running.
    ConcurrentDictionary<int, DtoCarDeletionProgress> CarDeletionProgresses { get; set; }
    //Keyed by the id of the charging station currently being deleted, so concurrent deletions do not clobber
    //each other's progress. An entry exists only while that station's deletion is running.
    ConcurrentDictionary<int, DtoChargingStationDeletionProgress> ChargingStationDeletionProgresses { get; set; }
    DateTimeOffset? StartupTime { get; set; }
    int LastPvDemoCase { get; set; }
    bool IsPreRelease { get; set; }

    /// <summary>
    /// Key is Id of the connector in database
    /// </summary>
    ConcurrentDictionary<int, DtoOcppConnectorState> OcppConnectorStates { get; set; }

    ConcurrentBag<DtoChargingSchedule> ChargingSchedules { get; set; }
    ConcurrentBag<NotChargingWithExpectedPowerReasonTemplate> GenericNotChargingWithExpectedPowerReasons { get; set; }
    ConcurrentDictionary<(int? carId, int? connectorId), List<NotChargingWithExpectedPowerReasonTemplate>> LoadPointSpecificNotChargingWithExpectedPowerReasons { get; set; }
    ConcurrentDictionary<int, (int? carId, DateTimeOffset combinationTimeStamp)> ManualSetLoadPointCarCombinations { get; set; }
    HashSet<DtoLoadpointCombination> LatestLoadPointCombinations { get; set; }
    int? LastLoggedHomeBatterySoc { get; set; }
    ConcurrentDictionary<int, DateTimeOffset> CarsWithNonZeroMeterValueAddedLastCycle { get; set; }
    ConcurrentDictionary<int, DateTimeOffset> ChargingConnectorsWithNonZeroMeterValueAddedLastCycle { get; set; }
    NextSunEvent NextSunEvent { get; set; }
    bool IsHomeBatteryDischargingActive { get; set; }
    DtoHomeBatterySocTarget? HomeBatteryHoldTarget { get; set; }
    DtoHomeBatterySocTarget? HomeBatteryChargeTarget { get; set; }
}
