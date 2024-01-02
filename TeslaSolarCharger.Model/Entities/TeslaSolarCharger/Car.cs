using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class Car
{
    public int Id { get; set; }
    public int TeslaMateCarId { get; set; }
    public TeslaCarFleetApiState TeslaFleetApiState { get; set; } = TeslaCarFleetApiState.NotConfigured;
}
