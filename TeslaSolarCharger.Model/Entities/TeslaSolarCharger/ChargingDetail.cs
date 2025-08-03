namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ChargingDetail
{
    public int Id { get; set; }
    public DateTime TimeStamp { get; set; }
    public int SolarPower { get; set; }
    public int HomeBatteryPower { get; set; }
    public int GridPower { get; set; }
    public int? ChargerVoltage { get; set; }
    public long? EstimatedCarTotalSolarEnergyWs { get; set; }
    public long? EstimatedCarTotalHomeBatteryEnergyWs { get; set; }
    public long? EstimatedCarTotalGridEnergyWs { get; set; }
    public long? EstimatedChargingConnectorTotalSolarEnergyWs { get; set; }
    public long? EstimatedChargingConnectorTotalHomeBatteryEnergyWs { get; set; }
    public long? EstimatedChargingConnectorTotalGridEnergyWs { get; set; }

    public int ChargingProcessId { get; set; }

    public ChargingProcess ChargingProcess { get; set; } = null!;
}
