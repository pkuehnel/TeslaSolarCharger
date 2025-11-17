using FluentValidation;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Solax;

public class DtoSolaxTemplateValueConfiguration
{
    public string? Host { get; set; }
    public string? Password { get; set; }
}

public class DtoSolaxTemplateValueConfgiurationValidator : AbstractValidator<DtoSolaxTemplateValueConfiguration>
{
    public DtoSolaxTemplateValueConfgiurationValidator()
    {
        RuleFor(x => x.Host).NotEmpty();
    }
}
