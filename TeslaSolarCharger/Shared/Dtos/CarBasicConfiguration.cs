using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;

namespace TeslaSolarCharger.Shared.Dtos;

public class CarBasicConfiguration
{
#pragma warning disable CS8618
    public CarBasicConfiguration(int carId, string? carName)
#pragma warning restore CS8618
    {
        CarId = carId;
        CarName = carName;
    }
    public int CarId { get; }
    public string? CarName { get; }
    [Range(1, int.MaxValue)]
    [Postfix("A")]
    public int MaximumAmpere { get; set; }
    [Range(1, int.MaxValue)]
    [Postfix("A")]
    public int MinimumAmpere { get; set; }
    [Range(1, int.MaxValue)]
    [Postfix("kWh")]
    public int UsableEnergy { get; set; }
    public bool ShouldBeManaged { get; set; } = true;
    public bool ShouldSetChargeStartTimes { get; set; }
    [Range(1, int.MaxValue)]
    public int ChargingPriority { get; set; }
    public string Vin { get; set; }
}
