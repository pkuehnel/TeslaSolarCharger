namespace SmartTeslaAmpSetter.Shared.Dtos.Settings;

public class CarState
{
    public string? Name { get; set; }
    public DateTime ShouldStartChargingSince { get; set; }
    public DateTime ShouldStopChargingSince { get; set; }
    public int SoC { get; set; }
    public int SocLimit { get; set; }
    public string? Geofence { get; set; }
    public TimeSpan TimeUntilFullCharge { get; set; }
    public bool AutoFullSpeedCharge { get; set; }
    public int LastSetAmp { get; set; }

    public int ChargingPowerAtHome { get; set; }

}