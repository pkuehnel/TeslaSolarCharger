namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoOcppConnectorState
{
    public DtoTimeStampedValue<bool> IsCharging { get; init; } = new(DateTimeOffset.MinValue, false);
    public DtoTimeStampedValue<bool> IsPluggedIn { get; init; } = new(DateTimeOffset.MinValue, false);
    public DtoTimeStampedValue<bool?> IsCarFullyCharged { get; init; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int> ChargingPower { get; init; } = new DtoTimeStampedValue<int>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<decimal> ChargingCurrent { get; init; } = new DtoTimeStampedValue<decimal>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<decimal> ChargingVoltage { get; init; } = new DtoTimeStampedValue<decimal>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<int?> PhaseCount { get; init; } = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, null);
}
