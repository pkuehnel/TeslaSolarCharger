namespace TeslaSolarCharger.Server.Dtos;

public class DtoRateLimitInfos
{
    public DateTime? VehicleRateLimitedUntil { get; set; }
    public DateTime? VehicleDataRateLimitedUntil { get; set; }
    public DateTime? CommandsRateLimitedUntil { get; set; }
    public DateTime? ChargingCommandsRateLimitedUntil { get; set; }
    public DateTime? WakeUpRateLimitedUntil { get; set; }
}
