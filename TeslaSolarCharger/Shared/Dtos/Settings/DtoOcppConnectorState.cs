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

    /// <summary>
    /// The phases to send to a charging connector so it does not switch phases while charging: phases may only change
    /// on a charge start after the phase switch cooldown. Connectors without automatic phase switching never get a
    /// number of phases as they might reject the message.
    /// </summary>
    /// <param name="autoSwitchBetween1And3PhasesEnabled">Whether the connector supports switching phases.</param>
    /// <param name="phasesIfUnknown">Used if TSC does not know the last set phases, e.g. after a restart.</param>
    public int? PhasesToKeepWhileCharging(bool autoSwitchBetween1And3PhasesEnabled, int? phasesIfUnknown)
    {
        return autoSwitchBetween1And3PhasesEnabled ? LastSetPhases.Value ?? phasesIfUnknown : null;
    }
}
