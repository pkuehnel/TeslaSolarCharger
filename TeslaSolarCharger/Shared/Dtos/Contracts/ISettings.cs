using System;
using System.Collections.Concurrent;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Contracts;

public interface ISettings
{
    ConcurrentDictionary<SolarDeviceKey, SolarDeviceState> SolarDevices { get; }

    double? GetAverageValue(ValueUsage usage, TimeSpan window, DateTimeOffset? now = null);

    int? InverterPower { get; }
    int? Overage { get; }
    int? HomeBatterySoc { get; }
    int? HomeBatteryPower { get; }
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
    DateTime StartupTime { get; set; }
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
}
