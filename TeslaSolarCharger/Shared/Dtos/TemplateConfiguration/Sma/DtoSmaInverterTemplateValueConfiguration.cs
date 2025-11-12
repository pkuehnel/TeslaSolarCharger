using FluentValidation;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

public class DtoSmaInverterTemplateValueConfiguration
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 502;
    public int UnitId { get; set; } = 3;
}

public class DtoSmaInverterTemplateValueConfigurationValidator : AbstractValidator<DtoSmaInverterTemplateValueConfiguration>
{
    public DtoSmaInverterTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.Host).NotEmpty();
        RuleFor(x => x.Port).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.Port).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Port).LessThanOrEqualTo(65535);
    }
}


public class DtoBaseSmaInverterTemplateValueConfiguration
    : DtoGenericTemplateValueConfiguration<DtoSmaInverterTemplateValueConfiguration>
{
    public new TemplateValueGatherType GatherType => TemplateValueGatherType.SmaInverterModbus;
}

public class DtoBaseSmaHybridInverterTemplateValueConfiguration
    : DtoGenericTemplateValueConfiguration<DtoSmaInverterTemplateValueConfiguration>
{
    public new TemplateValueGatherType GatherType => TemplateValueGatherType.SmaHybridInverterModbus;
}
