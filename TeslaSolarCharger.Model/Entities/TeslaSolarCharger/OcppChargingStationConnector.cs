namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class OcppChargingStationConnector
{
    public int Id { get; set; }
    public int ConnectorId { get; set; }

    public int OcppChargingStationId { get; set; }

    public OcppChargingStation OcppChargingStation { get; set; } = null!;
}
