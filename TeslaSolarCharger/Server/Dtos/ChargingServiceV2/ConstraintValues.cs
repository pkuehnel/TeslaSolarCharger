using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class ConstraintValues
{
    public int? MinCurrent { get; set; }
    public int? MaxCurrent { get; set; }
    public int? MinPhases { get; set; }
    public int? MaxPhases { get; set; }
    public ChargeModeV2? ChargeMode { get; set; }
    public int? MinSoc { get; set; }
    public int? MaxSoc { get; set; }
    public bool? PhaseReductionAllowed { get; set; }
    public bool? PhaseIncreaseAllowed { get; set; }
    public bool? PhaseSwitchingEnabled { get; set; }
    public bool? ChargeStopAllowed { get; set; }
    public bool? ChargeStartAllowed { get; set; }
    public DateTimeOffset? PhaseReductionAllowedAt { get; set; }
    public DateTimeOffset? PhaseIncreaseAllowedAt { get; set; }
    public DateTimeOffset? ChargeStopAllowedAt { get; set; }
    public DateTimeOffset? ChargeStartAllowedAt { get; set; }
    public int? Soc { get; set; }
    public int? CarSocLimit { get; set; }
    public bool? IsCharging { get; set; }
    public DateTimeOffset? LastIsChargingChange { get; set; }
    public bool? IsCarFullyCharged { get; set; }
    public bool? RequiresChargeStartDueToCarFullyChargedSinceLastCurrentSet { get; set; }
    public TimeSpan? PhaseSwitchCoolDownTime { get; set; }
}
