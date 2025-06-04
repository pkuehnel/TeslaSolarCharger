using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class DtoTimeZonedChargingTarget : CarChargingTarget
{
    public DateTimeOffset NextExecutionTime { get; set; }
}
