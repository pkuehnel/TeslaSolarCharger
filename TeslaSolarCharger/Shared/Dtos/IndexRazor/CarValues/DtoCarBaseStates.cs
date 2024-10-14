using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;

public class DtoCarBaseStates
{
    public int CarId { get; set; }
    public string? Name { get; set; }
    public string? Vin { get; set; }
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
    public bool ChargingNotPlannedDueToNoSpotPricesAvailable { get; set; }
    public TeslaCarFleetApiState? FleetApiState { get; set; }
    public List<DtoChargeInformation> ChargeInformation { get; set; } = new();
    public CarStateEnum? State { get; set; }
    public List<DtoChargingSlot> ChargingSlots { get; set; } = new();

    public DateTime? VehicleRateLimitedUntil { get; set; }
    public DateTime? VehicleDataRateLimitedUntil { get; set; }
    public DateTime? CommandsRateLimitedUntil { get; set; }
    public DateTime? ChargingCommandsRateLimitedUntil { get; set; }
    public DateTime? WakeUpRateLimitedUntil { get; set; }

    public DateTime? RateLimitedUntil
    {
        get
        {
            return new List<DateTime?>
            {
                VehicleRateLimitedUntil,
                VehicleDataRateLimitedUntil,
                CommandsRateLimitedUntil,
                ChargingCommandsRateLimitedUntil,
                WakeUpRateLimitedUntil,
            }.Max();
        }
    }
}
