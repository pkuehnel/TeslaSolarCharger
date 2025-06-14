using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoCar
{
    private int? _chargingPower;
    private int? _chargerActualCurrent;
    public int Id { get; set; }
    public string Vin { get; set; }
    public int? TeslaMateCarId { get; set; }

    public ChargeMode ChargeMode { get; set; }
    public ChargeModeV2 ChargeModeV2 { get; set; }

    public int MinimumSoC { get; set; }
    /// <summary>
    /// This field is always filled with local time, never with UTC time. The time gets converted to utc when writing to the database.
    /// </summary>
    public DateTime LatestTimeToReachSoC { get; set; }

    public bool IgnoreLatestTimeToReachSocDate { get; set; }
    public bool IgnoreLatestTimeToReachSocDateOnWeekend { get; set; }

    public int MaximumAmpere { get; set; }

    public int MinimumAmpere { get; set; }

    public int UsableEnergy { get; set; }

    public bool? ShouldBeManaged { get; set; } = true;

    public int ChargingPriority { get; set; }

    public string? Name { get; set; }
    public DtoTimeStampedValue<bool?> ShouldStartCharging { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> ShouldStopCharging { get; set; } = new(DateTimeOffset.MinValue, null);
    public DateTime? ShouldStartChargingSince { get; set; }
    public DateTime? EarliestSwitchOn { get; set; }
    public DateTime? ShouldStopChargingSince { get; set; }
    public DateTime? EarliestSwitchOff { get; set; }
    public DateTimeOffset? ScheduledChargingStartTime { get; set; }
    public int? SoC { get; set; }
    public int? SocLimit { get; set; }
    public bool? IsHomeGeofence { get; set; }
    public TimeSpan? TimeUntilFullCharge { get; set; }
    public DateTime? ReachingMinSocAtFullSpeedCharge { get; set; }
    public bool AutoFullSpeedCharge { get; set; }
    public DtoTimeStampedValue<int> LastSetAmp { get; set; } = new DtoTimeStampedValue<int>(DateTimeOffset.MinValue, 0);
    public int? ChargerPhases { get; set; }

    public int ActualPhases => ChargerPhases is null or > 1 ? 3 : 1;

    public int? ChargerVoltage { get; set; }

    public int? ChargerActualCurrent
    {
        get
        {
            if (_chargerActualCurrent > ChargerRequestedCurrent)
            {
                return ChargerRequestedCurrent;
            }
            return _chargerActualCurrent;
        }
        set => _chargerActualCurrent = value;
    }

    public int? ChargerPilotCurrent { get; set; }
    public int? ChargerRequestedCurrent { get; set; }
    public DtoTimeStampedValue<double?> MinBatteryTemperature { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<double?> MaxBatteryTemperature { get; set; } = new(DateTimeOffset.MinValue, null);

    public bool? PluggedIn { get; private set; }
    public DateTimeOffset? LastPluggedIn { get; set; }

    public void UpdatePluggedIn(DateTimeOffset timestamp, bool pluggedIn)
    {
        if (pluggedIn && (PluggedIn == false))
        {
            LastPluggedIn = timestamp;
        }
        if (!pluggedIn)
        {
            LastPluggedIn = default;
        }
        PluggedIn = pluggedIn;
    }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? DistanceToHomeGeofence { get; set; }

    public int? ChargingPowerAtHome
    {
        get
        {
            if (IsHomeGeofence == true)
            {
                return ChargingPower;
            }

            return 0;
        }
    }

    private int? ChargingPower
    {
        get
        {
            if (_chargingPower == default)
            {
                var actualCurrent = ChargerActualCurrent;
                if (actualCurrent > ChargerRequestedCurrent)
                {
                    actualCurrent = ChargerRequestedCurrent;
                }
                return actualCurrent * ChargerVoltage * ActualPhases;
            }
            return _chargingPower;
        }
        set => _chargingPower = value;
    }

    public CarStateEnum? State { get; set; }
    public bool? Healthy { get; set; }
    public bool ReducedChargeSpeedWarning { get; set; }
    public bool UseBle { get; set; }
    public string? BleApiBaseUrl { get; set; }
    public DateTime? EarliestHomeArrival { get; set; }
    public List<DtoChargingSlot> PlannedChargingSlots { get; set; } = new List<DtoChargingSlot>();
    public List<DateTime> WakeUpCalls { get; set; } = new List<DateTime>();
    public List<DateTime> VehicleDataCalls { get; set; } = new List<DateTime>();
    public List<DateTime> VehicleCalls { get; set; } = new List<DateTime>();
    public List<DateTime> ChargeStartCalls { get; set; } = new List<DateTime>();
    public List<DateTime> ChargeStopCalls { get; set; } = new List<DateTime>();
    public List<DateTime> SetChargingAmpsCall { get; set; } = new List<DateTime>();
    public List<DateTime> OtherCommandCalls { get; set; } = new List<DateTime>();

    public DateTime? LastNonSuccessBleCall { get; set; }
}
