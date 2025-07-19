namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoLoadPointWithCurrentChargingValues
{
    public int? CarId { get; set; }
    public int? ChargingConnectorId { get; set; }
    public int ChargingPower { get; set; }
    public int ChargingVoltage { get; set; }
    public decimal ChargingCurrent { get; set; }
    public int? ChargingPhases { get; set; }
}
