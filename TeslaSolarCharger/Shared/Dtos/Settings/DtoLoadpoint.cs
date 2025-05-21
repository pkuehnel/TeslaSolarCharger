namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoLoadpoint
{
    public DtoCar? Car { get; set; }
    public int? OcppConnectorId { get; set; }
    public DtoOcppConnectorState? OcppConnectorState { get; set; }

    public int? ActualChargingPower
    {
        get
        {
            if (OcppConnectorState != default)
            {
                return OcppConnectorState.ChargingPower.Value;
            }
            return Car?.ChargingPowerAtHome;
        }
    }
}
