namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ChargingStationConnectorAllowedCar
{
    public int CarId { get; set; }
    public int OcppChargingStationConnectorId { get; set; }

    public Car Car { get; set; }
    public OcppChargingStationConnector OcppChargingStationConnector { get; set; }
}
