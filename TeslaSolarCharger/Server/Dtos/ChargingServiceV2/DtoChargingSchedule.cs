namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class DtoChargingSchedule : ValidFromToBase
{
    public DtoChargingSchedule(int? carId, int? occpChargingConnectorId)
    {
        CarId = carId;
        OccpChargingConnectorId = occpChargingConnectorId;
    }

    //Required for ValidFromToSplitter
    public DtoChargingSchedule()
    {
    }

    public int? CarId { get; set; }
    public int? OccpChargingConnectorId { get; set; }
    public int? OnlyChargeOnAtLeastSolarPower { get; set; }
    public int ChargingPower { get; set; }
}
