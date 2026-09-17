using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// Schema version 1 of the persisted setup state, kept only so a state written by an older release can still be
/// read and migrated into <see cref="DtoSetupState"/>. Nothing writes this shape any more.
/// </summary>
public class DtoSetupCache
{
    public int CurrentStep { get; set; }
    public List<int> CompletedSteps { get; set; } = new();
    public bool HasPvSystem { get; set; }
    public bool HasHomeBattery { get; set; }
    public DtoBaseConfiguration Configuration { get; set; } = new();
    public DtoChargePrice? ChargePrice { get; set; }
    public List<FixedPrice> FixedPrices { get; set; } = new();
    public DtoCarsChargingSetupAnswers CarsChargingSetup { get; set; } = new();
}
