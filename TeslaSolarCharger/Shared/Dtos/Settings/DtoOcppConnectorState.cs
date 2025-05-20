namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoOcppConnectorState
{
    private DtoTimeStampedValue<bool> _isCharging = new(DateTimeOffset.MinValue, false);
    public DtoTimeStampedValue<bool> IsConnected { get; set; } = new(DateTimeOffset.MinValue, false);

    public DtoTimeStampedValue<bool> IsCharging
    {
        get => _isCharging;
    }

    public DtoTimeStampedValue<bool?> IsCarFullyCharged { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int> ChargingPower { get; set; } = new DtoTimeStampedValue<int>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<decimal> ChargingCurrent { get; set; } = new DtoTimeStampedValue<decimal>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<decimal> ChargingVoltage { get; set; } = new DtoTimeStampedValue<decimal>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<int?> PhaseCount { get; set; } = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, null);
    public DateTimeOffset? LastPluggedIn { get; private set; }

    public void UpdateIsCharging(DateTimeOffset timestamp, bool value)
    {
        if (value && (!_isCharging.Value))
        {
            LastPluggedIn = timestamp;
        }
        _isCharging = new DtoTimeStampedValue<bool>(timestamp, value);
    }
}
