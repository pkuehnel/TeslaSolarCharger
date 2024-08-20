using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;

namespace TeslaSolarCharger.Shared.Dtos;

public class CarBasicConfiguration
{
    public CarBasicConfiguration()
    {
    }

#pragma warning disable CS8618
    public CarBasicConfiguration(int id, string? name)
#pragma warning restore CS8618
    {
        Id = id;
        Name = name;
    }
    public int Id { get; set; }
    public string? Name { get; set; }
    public string Vin { get; set; }
    [Range(1, int.MaxValue)]
    [Postfix("A")]
    [HelperText("TSC never sets a current below this value")]
    public int MinimumAmpere { get; set; }
    [Range(1, int.MaxValue)]
    [Postfix("A")]
    [HelperText("TSC never sets a current above this value. This value is also used in the Max Power charge mode.")]
    public int MaximumAmpere { get; set; }
    [Range(1, int.MaxValue)]
    [Postfix("kWh")]
    [HelperText("This value is used to reach a desired SoC in time if on spot price or PVOnly charge mode.")]
    public int UsableEnergy { get; set; }
    [Range(1, int.MaxValue)]
    [HelperText("If there is not enough power for all cars, the cars will be charged ordered by priority. Cars with the same priority are ordered randomly.")]
    public int ChargingPriority { get; set; }
    [HelperText("If disabled, this car will not show up in the overview page and TSC does not manage it.")]
    public bool ShouldBeManaged { get; set; } = true;
    [HelperText("Use BLE communication to go around Tesla rate limits. Note: A BLE device (e.g. Raspberry Pi) with installed TeslaSolarChargerBle Container needs to be near your car.")]
    public bool UseBle { get; set; }
    [HelperText("Limits requests to car as getting values is rate limited.")]
    [Postfix("s")]
    [Range(11, int.MaxValue)]
    public int ApiRefreshIntervalSeconds { get; set; }
    [HelperText("Needed to send commands via BLE to the car. An example value would be `http://raspible:7210/`")]
    public string? BleApiBaseUrl { get; set; }

}
