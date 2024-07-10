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
    [HelperText("Enable this to use planned charges of your Tesla App. This ensures starting a planned charge even if the car can't be woken up via Tesla App.")]
    public bool ShouldSetChargeStartTimes { get; set; }
    [HelperText("Use BLE communication to go around Tesla rate limits. Note: A BLE device (e.g. Raspberry Pi) with installed TeslaSolarChargerBle Container needs to be near your car.")]
    public bool UseBle { get; set; }
    [HelperText("When enabling this setting, you need to delete the TSC Key in the car (is listed in Controls \u2192 Lock and should be called `unknown key`, do NOT delete the www.teslasolarcharger.de key) and execute BLE Pair again. Note: For now, this leads to a security leak where the BLE key has full control over your car, including unlocking and starting to drive, so if someone gets access to your BLE Device (either remote or physical) he could steal your car. This issue will be resolved in a future vehicle firmware update by Tesla. Currently, it is not known when this will be released.")]
    public bool UseBleForWakeUp { get; set; }
    [HelperText("Limits requests to car as getting values is rate limited.")]
    [Postfix("s")]
    [Range(11, int.MaxValue)]
    public int ApiRefreshIntervalSeconds { get; set; }
    [HelperText("Needed to send commands via BLE to the car. An example value would be `http://raspible:7210/`")]
    public string? BleApiBaseUrl { get; set; }
}
