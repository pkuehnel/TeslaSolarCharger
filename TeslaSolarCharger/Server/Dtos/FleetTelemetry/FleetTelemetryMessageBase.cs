using TeslaSolarCharger.Server.Enums;

namespace TeslaSolarCharger.Server.Dtos.FleetTelemetry;

public class FleetTelemetryMessageBase
{
    public FleetTelemetryMessageType MessageType { get; set; }
}
