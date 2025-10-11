namespace TeslaSolarCharger.Shared.Dtos;

public class DtoChargingSchedule : ValidFromToBase
{
    public DtoChargingSchedule(int? carId, int? ocppChargingConnectorId)
    {
        CarId = carId;
        OcppChargingConnectorId = ocppChargingConnectorId;
    }

    //Required for ValidFromToSplitter
    public DtoChargingSchedule()
    {
    }

    public int? CarId { get; set; }
    public int? OcppChargingConnectorId { get; set; }
    public int? OnlyChargeOnAtLeastSolarPower { get; set; }
    public int ChargingPower { get; set; }
    public int? TargetGridPower { get; set; }
}
