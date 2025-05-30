﻿using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DepricatedCarState
{
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

    private int ActualPhases => ChargerPhases is null or > 1 ? 3 : 1;

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
            float? currentToUse;
            //Next lines because of wrong actual current on currents below 5A
            if ((ChargerRequestedCurrent < 5) && (ChargerActualCurrent == (ChargerRequestedCurrent + 1)))
            {
                currentToUse = (float?)(ChargerActualCurrent + ChargerRequestedCurrent) / 2;
            }
            else
            {
                currentToUse = ChargerActualCurrent;
            }
            var power = (int?)(currentToUse * ChargerVoltage * ActualPhases);
            return power;
        }
    }
    public CarStateEnum? State { get; set; }
    public bool? Healthy { get; set; }
    public bool ReducedChargeSpeedWarning { get; set; }
    public List<DtoChargingSlot> PlannedChargingSlots { get; set; } = new List<DtoChargingSlot>();
}
