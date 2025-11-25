namespace TeslaSolarCharger.Shared.Dtos;

public class DtoChargingSchedule : ValidFromToBase
{
    public DtoChargingSchedule(int? carId, int? ocppChargingConnectorId, int maxPossiblePower, HashSet<ScheduleReason> scheduleReasons)
    {
        CarId = carId;
        OcppChargingConnectorId = ocppChargingConnectorId;
        MaxPossiblePower = maxPossiblePower;
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
    public int EstimatedSolarPower { get; set; }
    //Needs to be public for ValidFromToSplitter which clones properties
    // ReSharper disable once MemberCanBePrivate.Global
    public int MaxPossiblePower { get; set; }
    public HashSet<ScheduleReason> ScheduleReasons { get; set; } = new();

    public int EstimatedChargingPower
    {
        get
        {
            //next line only required because MathMax does not accept more than 2 parameters
            var estimatedNotRequiredPower = Math.Max(EstimatedSolarPower, TargetHomeBatteryPower ?? 0);
            var estimatedPower = Math.Max(TargetMinPower, estimatedNotRequiredPower);
            return Math.Min(MaxPossiblePower, estimatedPower);
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
