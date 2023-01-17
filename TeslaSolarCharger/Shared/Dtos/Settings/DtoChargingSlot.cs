namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoChargingSlot
{
    public DateTimeOffset ChargeStart { get; set; }
    public DateTimeOffset ChargeEnd { get; set; }
    public bool IsActive { get; set; }
    public TimeSpan ChargeDuration => ChargeEnd - ChargeStart;
}
