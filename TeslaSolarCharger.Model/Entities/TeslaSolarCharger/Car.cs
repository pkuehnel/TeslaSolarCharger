using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class Car
{
    public int Id { get; set; }
    public int? TeslaMateCarId { get; set; }
    public string? Name { get; set; }
    public string? Vin { get; set; }

    /// <summary>
    /// SmartCar's internal vehicle id. Known the moment a SmartCar connection exists (before the VIN),
    /// so it is the stable join key for SmartCar cars while the <see cref="Vin"/> backfills.
    /// </summary>
    public string? SmartCarVehicleId { get; set; }

    public TeslaCarFleetApiState? TeslaFleetApiState { get; set; }
    public bool IsFleetTelemetryHardwareIncompatible { get; set; }
    public ChargeModeV2 ChargeMode { get; set; }
    public CarType CarType { get; set; }
    public int MaximumPhases { get; set; }
    public int MinimumSoc { get; set; }
    public int MaximumSoc { get; set; }

    public int MaximumAmpere { get; set; }

    public int MinimumAmpere { get; set; }
    public int? SwitchOnAtCurrent { get; set; }
    public int? SwitchOffAtCurrent { get; set; }

    public int UsableEnergy { get; set; }

    public bool? ShouldBeManaged { get; set; }

    public int ChargingPriority { get; set; }

    public bool VehicleCommandProtocolRequired { get; set; }
    public bool UseBle { get; set; }
    public string? BleApiBaseUrl { get; set; }

    /// <summary>
    /// Send BLE commands of this car with debug logging enabled so the BLE container returns and logs the detailed
    /// tesla-control output. Enabled on the support page while troubleshooting a single car.
    /// </summary>
    public bool UseBleDebug { get; set; }
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
    public List<CarChargingTarget> CarChargingTargets { get; set; } = new();
}
