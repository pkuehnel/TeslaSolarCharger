using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class OcppChargingStationConnectorValueLog
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public OcppChargingStationConnectorValueType Type { get; set; }
    public bool BooleanValue { get; set; }

    public int OcppChargingStationConnectorId { get; set; }

    public OcppChargingStationConnector OcppChargingStationConnector { get; set; } = null!;
}
