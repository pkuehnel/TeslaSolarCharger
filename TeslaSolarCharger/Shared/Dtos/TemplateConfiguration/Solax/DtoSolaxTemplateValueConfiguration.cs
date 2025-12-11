using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Solax;

public class DtoSolaxTemplateValueConfiguration
{
    public string? Host { get; set; }
    public string? Password { get; set; }
}

public class DtoSolaxTemplateValueConfigurationValidator : AbstractValidator<DtoSolaxTemplateValueConfiguration>
{
    public DtoSolaxTemplateValueConfigurationValidator()
    {
        RuleFor(x => x.Host).NotEmpty();
    }
}
