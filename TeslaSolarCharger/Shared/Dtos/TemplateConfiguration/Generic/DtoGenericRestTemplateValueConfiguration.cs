using FluentValidation;
using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Helper;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

/// <summary>
/// Shared configuration for all vendors whose values are gathered via JSON REST APIs
/// (see GenericRestTemplateSettings for the per vendor defaults and JsonRestTemplateDefinitions for the value maps).
/// </summary>
public class DtoGenericRestTemplateValueConfiguration
{
    public string? Host { get; set; }
    public int Port { get; set; } = 80;
    public string? Username { get; set; }
    [DataType(DataType.Password)]
    public string? Password { get; set; }
    [DataType(DataType.Password)]
    public string? ApiToken { get; set; }
    public int DeviceId { get; set; }
    public bool EnableHomeBatteryControl { get; set; }
    [Postfix("W")]
    public int MaxBatteryChargePowerW { get; set; } = 3300;
}

public class DtoGenericRestTemplateValueConfigurationValidator : AbstractValidator<DtoGenericRestTemplateValueConfiguration>
{
    public DtoGenericRestTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.Host).NotEmpty();
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.MaxBatteryChargePowerW).GreaterThan(0).When(x => x.EnableHomeBatteryControl);
    }
}
