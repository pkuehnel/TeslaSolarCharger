using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoCar
{
    public int Id { get; set; }
    public string Vin { get; set; } = null!;
    public int? TeslaMateCarId { get; set; }

    public ChargeMode ChargeMode { get; set; }
    public ChargeModeV2 ChargeModeV2 { get; set; }

    public int MinimumSoC { get; set; }

    public int MaximumAmpere { get; set; }

    public int MinimumAmpere { get; set; }

    public int UsableEnergy { get; set; }

    public bool? ShouldBeManaged { get; set; } = true;

    public int ChargingPriority { get; set; }

    public string? Name { get; set; }
    public DtoTimeStampedValue<bool?> ShouldStartCharging { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> ShouldStopCharging { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int?> SoC { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int?> SocLimit { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> IsHomeGeofence { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int> LastSetAmp { get; set; } = new DtoTimeStampedValue<int>(DateTimeOffset.MinValue, 0);
    public DtoTimeStampedValue<int?> ChargerPhases { get; set; } = new(DateTimeOffset.MinValue, null);

    public int ActualPhases => ChargerPhases.Value is null or > 1 ? 3 : 1;

    public DtoTimeStampedValue<int?> ChargerVoltage { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int?> ChargerActualCurrent { get; set; } = new(DateTimeOffset.MinValue, null);

    public DtoTimeStampedValue<int?> ChargerPilotCurrent { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int?> ChargerRequestedCurrent { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<double?> MinBatteryTemperature { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<double?> MaxBatteryTemperature { get; set; } = new(DateTimeOffset.MinValue, null);

    public DtoTimeStampedValue<bool?> PluggedIn { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> IsCharging { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<bool?> IsOnline { get; set; } = new(DateTimeOffset.MinValue, null);

    public DtoTimeStampedValue<double?> Latitude { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<double?> Longitude { get; set; } = new(DateTimeOffset.MinValue, null);
    public DtoTimeStampedValue<int?> DistanceToHomeGeofence { get; set; } = new(DateTimeOffset.MinValue, null);

    public int? ChargingPowerAtHome
    {
        get
        {
            if (IsHomeGeofence.Value == true)
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
            var actualCurrent = ChargerActualCurrent.Value;
            if (actualCurrent > ChargerRequestedCurrent.Value)
            {
                actualCurrent = ChargerRequestedCurrent.Value;
            }
            return actualCurrent * ChargerVoltage.Value * ActualPhases;
        }
    }

    public bool UseBle { get; set; }
    public string? BleApiBaseUrl { get; set; }
    public DtoTimeStampedValue<DateTime?> EarliestHomeArrival { get; set; } = new(DateTimeOffset.MinValue, null);
    public List<DateTime> WakeUpCalls { get; set; } = new List<DateTime>();
    public List<DateTime> VehicleDataCalls { get; set; } = new List<DateTime>();
    public List<DateTime> VehicleCalls { get; set; } = new List<DateTime>();
    public List<DateTime> ChargeStartCalls { get; set; } = new List<DateTime>();
    public List<DateTime> ChargeStopCalls { get; set; } = new List<DateTime>();
    public List<DateTime> SetChargingAmpsCall { get; set; } = new List<DateTime>();
    public List<DateTime> OtherCommandCalls { get; set; } = new List<DateTime>();

    public DateTime? LastNonSuccessBleCall { get; set; }
}
