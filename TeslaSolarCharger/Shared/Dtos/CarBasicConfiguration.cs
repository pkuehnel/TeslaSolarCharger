using FluentValidation;
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
    public string Vin { get; set; }

    [Postfix("A")]
    public int MinimumAmpere { get; set; } = 6;

    [Postfix("A")]
    public int MaximumAmpere { get; set; } = 16;
    [Postfix("A")]
    public int? SwitchOffAtCurrent { get; set; }
    [Postfix("A")]
    public int? SwitchOnAtCurrent { get; set; }
    [Postfix("kWh")]
    public int UsableEnergy { get; set; }
    public int ChargingPriority { get; set; }
    public bool ShouldBeManaged { get; set; } = true;
    public bool UseBle { get; set; }
    public string? BleApiBaseUrl { get; set; }
    public bool UseFleetTelemetry { get; set; }
    public bool IncludeTrackingRelevantFields { get; set; }
    public HomeDetectionVia HomeDetectionVia { get; set; }
    public CarType CarType { get; set; }
    public int MaximumPhases { get; set; }
}


public class CarBasicConfigurationValidator : AbstractValidator<CarBasicConfiguration>
{
    public CarBasicConfigurationValidator()
    {
        When(x => x.ShouldBeManaged, () =>
        {
            RuleFor(x => x.MinimumAmpere).GreaterThanOrEqualTo(0);
            RuleFor(x => x.MaximumAmpere).GreaterThan(0);
            RuleFor(x => x.MaximumAmpere).LessThanOrEqualTo(64);
            RuleFor(x => x.MaximumPhases).GreaterThan(0).LessThanOrEqualTo(3);
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Vin).NotEmpty();
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
            RuleFor(x => x.BleApiBaseUrl)
                .Must(uri => string.IsNullOrEmpty(uri) || (Uri.TryCreate(uri, UriKind.Absolute, out var outUri) && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps)))
                .WithMessage("BLE API Base URL must be a valid HTTP or HTTPS URL.");
        });
        
    }
}
