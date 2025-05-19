namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoOcppConnectorState
{
    public DtoTimeStampedValue<bool> IsConnected { get; set; } = new(DateTimeOffset.MinValue, false);
    public DtoTimeStampedValue<bool> IsCharging { get; set; } = new(DateTimeOffset.MinValue, false);
    public DtoTimeStampedValue<bool?> IsCarFullyCharged { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int> ChargingPower { get; set; } = new DtoTimeStampedValue<int>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<decimal> ChargingCurrent { get; set; } = new DtoTimeStampedValue<decimal>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<decimal> ChargingVoltage { get; set; } = new DtoTimeStampedValue<decimal>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<int?> PhaseCount { get; set; } = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, null);
}
