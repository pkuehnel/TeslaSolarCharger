using FluentValidation;
using System.ComponentModel;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Enums;

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
    [Disabled]
    public string Vin { get; set; }
    [Postfix("A")]
    [HelperText("TSC never sets a current below this value")]
    public int MinimumAmpere { get; set; }
    [Postfix("A")]
    [HelperText("TSC never sets a current above this value. This value is also used in the Max Power charge mode.")]
    public int MaximumAmpere { get; set; }
    [Postfix("A")]
    [HelperText("The charging point will stop charging when the available current drops below this value. This allows charging to continue for a while even if the current dips slightly, preventing unnecessary interruptions. Note: If you set this value to e.g. 3A while Min Current is set to 6A, charging will continue with 6A as long as there is enough solar power for 3A.")]
    public int? SwitchOffAtCurrent { get; set; }
    [Postfix("A")]
    [HelperText("The charging point will only begin charging when the available current exceeds this value. Helps to avoid starting the charging process too frequently due to small current fluctuations.")]
    public int? SwitchOnAtCurrent { get; set; }
    [Postfix("kWh")]
    [HelperText("This value is used to reach a desired SoC in time if on spot price or PVOnly charge mode.")]
    public int UsableEnergy { get; set; }
    [HelperText("If there is not enough power for all cars, the cars will be charged ordered by priority. Cars with the same priority are ordered randomly.")]
    public int ChargingPriority { get; set; }
    [HelperText("If disabled, this car will not show up in the overview page and TSC does not manage it.")]
    public bool ShouldBeManaged { get; set; } = true;
    [DisplayName("Use BLE")]
    [HelperText("Use BLE communication (If enabled no car license is required for this car). Note: A BLE device (e.g., Raspberry Pi) with installed TeslaSolarChargerBle Container needs to be near (max 4 meters without any walls in between) your car.")]
    public bool UseBle { get; set; }
    [HelperText("Needed to send commands via BLE to the car. An example value would be `http://raspible:7210/`")]
    public string? BleApiBaseUrl { get; set; }
    [HelperText("Only supported on cars with Software 2024.45.32+. Not supported on Pre 2021 Model S/X. If your car does not support fleet telemetry, this option will be disabled automatically within two minutes.")]
    public bool UseFleetTelemetry { get; set; }

    [HelperText("When enabled, TSC collects data of additional fields that are not necessarily required for TSC to work, but logged data might be helpful for future visualizations. Note: For this a car license is required.")]
    public bool IncludeTrackingRelevantFields { get; set; }
    public HomeDetectionVia HomeDetectionVia { get; set; }
}


public class CarBasicConfigurationValidator : AbstractValidator<CarBasicConfiguration>
{
    public CarBasicConfigurationValidator()
    {
        When(x => x.ShouldBeManaged, () =>
        {
            RuleFor(x => x.MinimumAmpere).GreaterThan(0);
            RuleFor(x => x.MaximumAmpere).GreaterThan(0);
            RuleFor(x => x.MaximumAmpere).LessThanOrEqualTo(64);
            RuleFor(x => x)
                .Must(config => config.MaximumAmpere >= config.MinimumAmpere)
                .WithMessage("Maximum Ampere must be greater than or equal to Minimum Ampere.");
            RuleFor(x => x.UsableEnergy).GreaterThan(5);
            RuleFor(x => x.ChargingPriority).GreaterThan(0);
            When(x => x.SwitchOnAtCurrent != default && x.SwitchOffAtCurrent != default, () =>
            {
                RuleFor(x => x)
                    .Must(config => config.SwitchOnAtCurrent >= config.SwitchOffAtCurrent)
                    .WithMessage("Switch On At Current must be greater than or equal to Switch Off At Current.");
            });
        });
        
    }
}
