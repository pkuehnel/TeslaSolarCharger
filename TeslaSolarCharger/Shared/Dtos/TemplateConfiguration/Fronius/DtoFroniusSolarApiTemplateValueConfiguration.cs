using FluentValidation;
using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Fronius;

public class DtoFroniusSolarApiTemplateValueConfiguration
{
    public string? Host { get; set; }
    /// <summary>
    /// When enabled TSC can block discharging of the home battery via the inverter's time of use configuration.
    /// Forced charging is not supported by the Solar API.
    /// </summary>
    public bool EnableHomeBatteryControl { get; set; }
    public string Username { get; set; } = "customer";
    [DataType(DataType.Password)]
    public string? Password { get; set; }
    /// <summary>
    /// Firmware versions starting with 1.36.5-1 use /api/config instead of /config for the battery configuration.
    /// </summary>
    public bool UseApiConfigPath { get; set; }
}

public class DtoFroniusSolarApiTemplateValueConfigurationValidator : AbstractValidator<DtoFroniusSolarApiTemplateValueConfiguration>
{
    public DtoFroniusSolarApiTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.Host).NotEmpty();
        RuleFor(x => x.Username).NotEmpty().When(x => x.EnableHomeBatteryControl);
        RuleFor(x => x.Password).NotEmpty().When(x => x.EnableHomeBatteryControl);
    }
}
