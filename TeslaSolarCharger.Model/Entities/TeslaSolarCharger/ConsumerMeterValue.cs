namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ConsumerMeterValue
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public int? MeasuredPowerW { get; set; }
    public long? MeasuredEnergyWs { get; set; }
    public int? EstimatedPowerW { get; set; }
    public long? EstimatedEnergyWs { get; set; }

    public int? OcppChargingStationConnectorId { get; set; }
    public int? CarId { get; set; }

    public OcppChargingStationConnector? OcppChargingStationConnector { get; set; }
    public Car? Car { get; set; }
}
