using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class Car
{
    public int Id { get; set; }
    public int? TeslaMateCarId { get; set; }
    public string? Name { get; set; }
    public string? Vin { get; set; }
    public TeslaCarFleetApiState TeslaFleetApiState { get; set; } = TeslaCarFleetApiState.NotConfigured;
    public ChargeMode ChargeMode { get; set; }
    public int MinimumSoc { get; set; }
    public DateTime LatestTimeToReachSoC { get; set; }

    public bool IgnoreLatestTimeToReachSocDate { get; set; }

    public int MaximumAmpere { get; set; }

    public int MinimumAmpere { get; set; }

    public int UsableEnergy { get; set; }

    public bool? ShouldBeManaged { get; set; }
    public bool? ShouldSetChargeStartTimes { get; set; }

    public int ChargingPriority { get; set; }

    public int? SoC { get; set; }
    public int? SocLimit { get; set; }

    public int? ChargerPhases { get; set; }
    public int? ChargerVoltage { get; set; }
    public int? ChargerActualCurrent { get; set; }
    public int? ChargerPilotCurrent { get; set; }
    public int? ChargerRequestedCurrent { get; set; }
    public bool? PluggedIn { get; set; }
    public bool? ClimateOn { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public CarStateEnum? State { get; set; }
    public bool VehicleCommandProtocolRequired { get; set; }
    public DateTime? RateLimitedUntil { get; set; }
    public bool UseBle { get; set; }
    public int ApiRefreshIntervalSeconds { get; set; }

    public List<ChargingProcess> ChargingProcesses { get; set; } = new List<ChargingProcess>();
}
