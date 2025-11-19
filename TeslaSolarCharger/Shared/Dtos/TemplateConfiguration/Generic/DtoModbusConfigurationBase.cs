using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

public abstract class DtoModbusConfigurationBase
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public int UnitId { get; set; }
}

public class DtoModbusConfigurationBaseValidator : AbstractValidator<DtoModbusConfigurationBase>
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
