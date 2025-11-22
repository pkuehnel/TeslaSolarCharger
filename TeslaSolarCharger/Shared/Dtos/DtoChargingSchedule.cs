namespace TeslaSolarCharger.Shared.Dtos;

public class DtoChargingSchedule : ValidFromToBase
{
    public DtoChargingSchedule(int? carId, int? ocppChargingConnectorId, int maxPossiblePower)
    {
        CarId = carId;
        OcppChargingConnectorId = ocppChargingConnectorId;
        MaxPossiblePower = maxPossiblePower;
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
    private int MaxPossiblePower { get; init; }

    public int EstimatedChargingPower
    {
        get
        {
            var estimatedPower = Math.Max(TargetMinPower, EstimatedSolarPower);
            return Math.Min(MaxPossiblePower, estimatedPower);
        }
    }
}
