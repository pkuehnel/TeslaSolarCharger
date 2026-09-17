namespace TeslaSolarCharger.Shared.Dtos;

public class DtoChargingSchedule : ValidFromToBase
{
    public DtoChargingSchedule(int? carId, int? ocppChargingConnectorId, int maxPossiblePower, int voltage, int phases, HashSet<ScheduleReason> scheduleReasons)
    {
        CarId = carId;
        OcppChargingConnectorId = ocppChargingConnectorId;
        MaxPossiblePower = maxPossiblePower;
        Voltage = voltage;
        Phases = phases;
        ScheduleReasons = scheduleReasons;
    }

    //Required for ValidFromToSplitter
    public DtoChargingSchedule()
    {
    }

    public int? CarId { get; set; }
    public int? OcppChargingConnectorId { get; set; }
    public int TargetMinPower { get; set; }
    public int? TargetHomeBatteryPower { get; set; }
    /// <summary>
    /// Part of <see cref="TargetHomeBatteryPower"/> that is expected to reach the car. The rest of the home battery
    /// discharge power is consumed by the house, so energy planning must not credit it to the car. Falls back to
    /// <see cref="TargetHomeBatteryPower"/> when not set.
    /// </summary>
    public int? EstimatedHomeBatteryPowerForCar { get; set; }
    public int EstimatedSolarPower { get; set; }
    //Needs to be public for ValidFromToSplitter which clones properties
    // ReSharper disable once MemberCanBePrivate.Global
    public int MaxPossiblePower { get; set; }
    //Setter is required for ValidFromToSplitter which clones properties
    public int Voltage { get; set; }
    public int Phases { get; set; }
    public HashSet<ScheduleReason> ScheduleReasons { get; set; } = new();

    public int EstimatedChargingPower
    {
        get
        {
            var homeBatteryPowerForCar = EstimatedHomeBatteryPowerForCar ?? TargetHomeBatteryPower ?? 0;
            //next line only required because MathMax does not accept more than 2 parameters
            var estimatedNotRequiredPower = Math.Max(EstimatedSolarPower, homeBatteryPowerForCar);
            var estimatedPower = Math.Max(TargetMinPower, estimatedNotRequiredPower);
            return Math.Min(MaxPossiblePower, estimatedPower);
        }
    }

    public int EstimatedEnergy
    {
        get
        {
            var duration = ValidTo - ValidFrom;
            return (int) (EstimatedChargingPower * duration.TotalHours);
        }
    }
}

public enum ScheduleReason
{
    ExpectedSolarProduction,
    HomeBatteryDischarging,
    CheapGridPrice,
    BridgeSchedules,
    LatestPossibleTime,
}
