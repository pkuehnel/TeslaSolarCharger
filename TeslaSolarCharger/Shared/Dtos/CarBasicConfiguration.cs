using System.ComponentModel.DataAnnotations;

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
    public int MaximumAmpere { get; set; }
    public int MinimumAmpere { get; set; }
    public int UsableEnergy { get; set; }
    public bool? ShouldBeManaged { get; set; }
    public bool ShouldSetChargeStartTimes { get; set; }
    [Range(1, int.MaxValue)]
    public int ChargingPriority { get; set; }
    public string VehicleIdentificationNumber { get; set; }
}
