using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

public abstract class DtoModbusConfigurationBase
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public int UnitId { get; set; }
}

/// <summary>
/// Validator resolution (Blazilla on the client) looks up IValidator&lt;T&gt; for the exact model type without
/// considering base classes, so this validator is generic: every DTO deriving from
/// <see cref="DtoModbusConfigurationBase"/> that is edited in a form needs its own subclass of this validator
/// (see e.g. DtoKostalModbusConfigurationValidator), which the assembly scan then registers for the exact type.
/// </summary>
public class DtoModbusConfigurationBaseValidator<T> : AbstractValidator<T> where T : DtoModbusConfigurationBase
{
    public DtoModbusConfigurationBaseValidator()
    {
        RuleFor(x => x.Host).NotEmpty();
        RuleFor(x => x.Port).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.Port).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Port).LessThanOrEqualTo(65535);
    }
}
