namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoLoadPointOverview
{
    public int? CarId { get; set; }
    public string? CarName { get; set; }
    public int? ChargingConnectorId { get; set; }
    public string? ChargingConnectorName { get; set; }
    public int ChargingPower { get; set; }
    public int? MaxPhaseCount { get; set; }
    public int? ChargingPhaseCount { get; set; }
    public int MaxCurrent { get; set; }
    public decimal ChargingCurrent { get; set; }
}
