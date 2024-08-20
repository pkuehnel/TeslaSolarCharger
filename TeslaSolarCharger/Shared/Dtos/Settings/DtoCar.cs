using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoCar
{
    private int? _chargingPower;
    public int Id { get; set; }
    public string Vin { get; set; }
    public int? TeslaMateCarId { get; set; }

    public ChargeMode ChargeMode { get; set; }

    public int MinimumSoC { get; set; }
    /// <summary>
    /// This field is always filled with local time, never with UTC time. The time gets converted to utc when writing to the database.
    /// </summary>
    public DateTime LatestTimeToReachSoC { get; set; }

    public bool IgnoreLatestTimeToReachSocDate { get; set; }

    public int MaximumAmpere { get; set; }

    public int MinimumAmpere { get; set; }

    public int UsableEnergy { get; set; }

    public bool? ShouldBeManaged { get; set; } = true;

    public int ChargingPriority { get; set; }

    public string? Name { get; set; }
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
    public int LastSetAmp { get; set; }
    public int? ChargerPhases { get; set; }

    public int ActualPhases => ChargerPhases is null or > 1 ? 3 : 1;

    public int? ChargerVoltage { get; set; }
    public int? ChargerActualCurrent { get; set; }
    public int? ChargerPilotCurrent { get; set; }
    public int? ChargerRequestedCurrent { get; set; }
    public bool? PluggedIn { get; set; }
    public bool? ClimateOn { get; set; }
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
                return ChargerActualCurrent * ChargerVoltage * ActualPhases;
            }
            return _chargingPower;
        }
        set => _chargingPower = value;
    }

    public CarStateEnum? State { get; set; }
    public bool? Healthy { get; set; }
    public bool ReducedChargeSpeedWarning { get; set; }
    public DateTimeOffset LastApiDataRefresh { get; set; }
    public int ApiRefreshIntervalSeconds { get; set; }
    public bool UseBle { get; set; }
    public string? BleApiBaseUrl { get; set; }
    public List<DtoChargingSlot> PlannedChargingSlots { get; set; } = new List<DtoChargingSlot>();
    public List<DateTime> WakeUpCalls { get; set; } = new List<DateTime>();
    public List<DateTime> VehicleDataCalls { get; set; } = new List<DateTime>();
    public List<DateTime> VehicleCalls { get; set; } = new List<DateTime>();
    public List<DateTime> ChargeStartCalls { get; set; } = new List<DateTime>();
    public List<DateTime> ChargeStopCalls { get; set; } = new List<DateTime>();
    public List<DateTime> SetChargingAmpsCall { get; set; } = new List<DateTime>();
    public List<DateTime> OtherCommandCalls { get; set; } = new List<DateTime>();
}
