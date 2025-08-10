using TeslaSolarCharger.Model.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class MeterValue
{
    public MeterValue(DateTimeOffset timestamp, MeterValueKind meterValueKind, int measuredPower)
    {
        Timestamp = timestamp;
        MeterValueKind = meterValueKind;
        MeasuredPower = measuredPower;
    }
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public MeterValueKind MeterValueKind { get; set; }
    public int MeasuredPower { get; set; }
    public long? EstimatedEnergyWs { get; set; }
    public int MeasuredHomeBatteryPower { get; set; }
    public int MeasuredGridPower { get; set; }
    public long? EstimatedHomeBatteryEnergyWs { get; set; }
    public long? EstimatedGridEnergyWs { get; set; }

    public int? CarId { get; set; }
    public int? ChargingConnectorId { get; set; }

    public Car? Car { get; set; }
    public OcppChargingStationConnector? ChargingConnector { get; set; }
}
