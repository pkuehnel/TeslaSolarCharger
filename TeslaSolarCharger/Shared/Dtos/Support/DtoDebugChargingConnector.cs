namespace TeslaSolarCharger.Shared.Dtos.Support;

public class DtoDebugChargingConnector
{
    public DtoDebugChargingConnector(string chargePointId)
    {
        ChargePointId = chargePointId;
    }

    public int ConnectorId { get; set; }
    public string ChargePointId { get; set; }
}
