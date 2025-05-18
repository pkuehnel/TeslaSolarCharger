namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class OcppTransaction
{
    public int Id { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int ChargingStationConnectorId { get; set; }

    public OcppChargingStationConnector ChargingStationConnector { get; set; }
}
