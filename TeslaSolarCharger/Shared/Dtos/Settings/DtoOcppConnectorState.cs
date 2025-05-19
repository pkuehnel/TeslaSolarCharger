namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoOcppConnectorState
{
    public bool IsConnected { get; set; }
    public bool IsCharging { get; set; }
    public bool? IsCarFullyCharged { get; set; }
    public int ChargingPower { get; set; }
}
