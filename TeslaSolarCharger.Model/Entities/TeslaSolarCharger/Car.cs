using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class Car
{
    public int Id { get; set; }
    public int? TeslaMateCarId { get; set; }
    public string? Name { get; set; }
    public string? Vin { get; set; }
    public TeslaCarFleetApiState? TeslaFleetApiState { get; set; }
    public bool IsFleetTelemetryHardwareIncompatible { get; set; }
    public ChargeMode ChargeMode { get; set; }
    public int MinimumSoc { get; set; }
    public DateTime LatestTimeToReachSoC { get; set; }

    public bool IgnoreLatestTimeToReachSocDate { get; set; }
    public bool IgnoreLatestTimeToReachSocDateOnWeekend { get; set; }

    public int MaximumAmpere { get; set; }

    public int MinimumAmpere { get; set; }

    public int UsableEnergy { get; set; }

    public bool? ShouldBeManaged { get; set; }

    public int ChargingPriority { get; set; }

    public int? SoC { get; set; }
    public int? SocLimit { get; set; }
    public int? ChargerPhases { get; set; }
    public int? ChargerVoltage { get; set; } //NotAvailabel in Fleet Telemetry
    public int? ChargerActualCurrent { get; set; }
    public int? ChargerPilotCurrent { get; set; }
    public int? ChargerRequestedCurrent { get; set; }
    public bool? PluggedIn { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public CarStateEnum? State { get; set; }

    public bool VehicleCommandProtocolRequired { get; set; }
    public bool UseBle { get; set; }
    public string? BleApiBaseUrl { get; set; }
    public bool UseFleetTelemetry { get; set; }
    public bool IncludeTrackingRelevantFields { get; set; }
    public bool IsAvailableInTeslaAccount { get; set; }
    public HomeDetectionVia HomeDetectionVia { get; set; }

    public List<DateTime> WakeUpCalls { get; set; } = new();
    public List<DateTime> VehicleDataCalls { get; set; } = new();
    public List<DateTime> VehicleCalls { get; set; } = new();
    public List<DateTime> ChargeStartCalls { get; set; } = new();
    public List<DateTime> ChargeStopCalls { get; set; } = new();
    public List<DateTime> SetChargingAmpsCall { get; set; } = new();
    public List<DateTime> OtherCommandCalls { get; set; } = new();

    public List<ChargingProcess> ChargingProcesses { get; set; } = new List<ChargingProcess>();
    public List<CarValueLog> CarValueLogs { get; set; } = new List<CarValueLog>();
    public List<CarChargingSchedule> ChargingSchedules { get; set; } = new();
}
