namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class DtoAvailablePowerSources
{
    public bool InverterPowerAvailable { get; set; }
    public bool GridPowerAvailable { get; set; }
    public bool HomeBatteryPowerAvailable { get; set; }
    public bool HomePowerAvailable => InverterPowerAvailable && GridPowerAvailable;
}
