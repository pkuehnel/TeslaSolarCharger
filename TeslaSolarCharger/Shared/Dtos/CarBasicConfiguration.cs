namespace TeslaSolarCharger.Shared.Dtos;

public class CarBasicConfiguration
{
    public CarBasicConfiguration(int carId, string? carName)
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
    public int ChargingPriority { get; set; }
    public string VehicleIdentificationNumber { get; set; }
}
