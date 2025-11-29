using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.TeslaPowerwall;

public class DtoTeslaPowerwallTemplateValueConfiguration
{
    public long? EnergySiteId { get; set; }
}


public class DtoTeslaPowerwallTemplateValueConfigurationValidator : AbstractValidator<DtoTeslaPowerwallTemplateValueConfiguration>
{
    public DtoTeslaPowerwallTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.EnergySiteId).NotEmpty();
    }
}
