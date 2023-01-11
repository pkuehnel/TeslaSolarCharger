using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;

public class DtoCarBaseStates
{
    public int CarId { get; set; }
    public string? NameOrVin { get; set; }
    public int? StateOfCharge { get; set; }
    public int? StateOfChargeLimit { get; set; }
    public int? HomeChargePower { get; set; }
#pragma warning disable CS8618
    public DtoChargeSummary DtoChargeSummary { get; set; }
#pragma warning restore CS8618
    public bool PluggedIn { get; set; }
    public bool IsHome { get; set; }
    public bool IsAutoFullSpeedCharging { get; set; }
    public bool? IsHealthy { get; set; }
}
