using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ChargerValueLog
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public ChargerValueType Type { get; set; }
    public int IntValue { get; set; }

    public int OcppChargingStationConnectorId { get; set; }
    public OcppChargingStationConnector OcppChargingStationConnector { get; set; } = null!;
}
