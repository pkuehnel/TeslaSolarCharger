namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class CarState
{
    public string? Name { get; set; }
    public DateTime ShouldStartChargingSince { get; set; }
    public DateTime ShouldStopChargingSince { get; set; }
    public int? SoC { get; set; }
    public int? SocLimit { get; set; }
    public string? Geofence { get; set; }
    public TimeSpan? TimeUntilFullCharge { get; set; }
    public DateTime? ReachingMinSocAtFullSpeedCharge { get; set; }
    public bool AutoFullSpeedCharge { get; set; }
    public int LastSetAmp { get; set; }
    public int? ChargerPhases { get; set; }

    public int? ActualPhases => ChargerPhases > 1 ? 3 : 1;

    public int? ChargerVoltage { get; set; }
    public int? ChargerActualCurrent { get; set; }
    public int? ChargerPilotCurrent { get; set; }
    public int? ChargerRequestedCurrent { get; set; }
    public bool? PluggedIn { get; set; }
    public bool? ClimateOn { get; set; }
    public int? ChargingPowerAtHome { get; set; }
    public int? ChargingPower
    {
        get
        {
            var power = ChargerActualCurrent * ChargerVoltage * ActualPhases;
            return power;
        }
    }

    public string? StateString { get; set; }
    public Enums.CarState? State { get; set; }
    public bool? Healthy { get; set; }
    public bool ReducedChargeSpeedWarning { get; set; }
}