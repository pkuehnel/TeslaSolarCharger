namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoOcppConnectorState
{
    public DtoTimeStampedValue<bool> IsCharging { get; init; } = new(DateTimeOffset.MinValue, false);
    public DtoTimeStampedValue<bool> IsPluggedIn { get; init; } = new(DateTimeOffset.MinValue, false);
    public DtoTimeStampedValue<bool?> IsCarFullyCharged { get; init; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int> ChargingPower { get; init; } = new(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<decimal> ChargingCurrent { get; init; } = new(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<decimal> ChargingVoltage { get; init; } = new(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<int?> PhaseCount { get; init; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<decimal?> LastSetCurrent { get; init; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int?> LastSetPhases { get; init; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> ShouldStartCharging { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> ShouldStopCharging { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> CanHandlePowerOnThreePhase { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> CanHandlePowerOnOnePhase { get; set; } = new(DateTimeOffset.MinValue, null);
}
