namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class DtoChargingSchedule
{
    public DtoChargingSchedule(int? carId, int? occpChargingConnectorId)
    {
        CarId = carId;
        OccpChargingConnectorId = occpChargingConnectorId;
    }

    public int? CarId { get; set; }
    public int? OccpChargingConnectorId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    public int ChargingPower { get; set; }
}
