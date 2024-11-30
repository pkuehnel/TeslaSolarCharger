using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class Car
{
    public int Id { get; set; }
    public int? TeslaMateCarId { get; set; }
    public string? Name { get; set; }
    public string? Vin { get; set; }
    public TeslaCarFleetApiState? TeslaFleetApiState { get; set; }
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
    public bool? ClimateOn { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public CarStateEnum? State { get; set; }

    public bool VehicleCommandProtocolRequired { get; set; }
    public DateTime? VehicleRateLimitedUntil { get; set; }
    public DateTime? VehicleDataRateLimitedUntil { get; set; }
    public DateTime? CommandsRateLimitedUntil { get; set; }
    public DateTime? WakeUpRateLimitedUntil { get; set; }
    public DateTime? ChargingCommandsRateLimitedUntil { get; set; }
    public bool UseBle { get; set; }
    public bool UseBleForWakeUp { get; set; }
    public string? BleApiBaseUrl { get; set; }
    public bool UseFleetTelemetry { get; set; }
    public bool UseFleetTelemetryForLocationData { get; set; }

    public string? WakeUpCalls { get; set; }
    public string? VehicleDataCalls { get; set; }
    public string? VehicleCalls { get; set; }
    public string? ChargeStartCalls { get; set; }
    public string? ChargeStopCalls { get; set; }
    public string? SetChargingAmpsCall { get; set; }
    public string? OtherCommandCalls { get; set; }

    public List<ChargingProcess> ChargingProcesses { get; set; } = new List<ChargingProcess>();
    public List<CarValueLog> CarValueLogs { get; set; } = new List<CarValueLog>();
}
