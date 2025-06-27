namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoLoadPointOverview
{
    public int? CarId { get; set; }
    public int? ChargingConnectorId { get; set; }
    public decimal? ActualCurrent { get; set; }
    public int? MaxCurrent { get; set; }
    public int? MinCurrent { get; set; }
    public int? ActualPhases { get; set; }
    public int? MaxPhases { get; set; }
    public int? ChargingPower { get; set; }
    public bool? IsHome { get; set; }
    public bool? IsPluggedIn { get; set; }
    public int? ChargingPriority { get; set; }
    public bool ManageChargingPowerByCar { get; set; }
}
