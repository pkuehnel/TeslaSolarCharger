namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ChargingProcess
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? UsedGridEnergyKwh { get; set; }
    public decimal? UsedHomeBatteryEnergyKwh { get; set; }
    public decimal? UsedSolarEnergyKwh { get; set; }
    public decimal? Cost { get; set; }
    public int? OldHandledChargeId { get; set; }

    public int? CarId { get; set; }
    public int? OcppChargingStationConnectorId { get; set; }

    public Car? Car { get; set; }
    public OcppChargingStationConnector? OcppChargingStationConnector { get; set; }

    public List<ChargingDetail> ChargingDetails { get; set; } = new();
}
