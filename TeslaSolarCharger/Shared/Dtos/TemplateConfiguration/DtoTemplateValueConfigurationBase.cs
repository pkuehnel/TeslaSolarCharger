using FluentValidation;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

public class DtoTemplateValueConfigurationBase
{
    public  int Id { get; set; }
    public string? Name { get; set; }
    public int ConfigurationVersion { get; set; }
    public int? MinRefreshIntervalMilliseconds { get; set; }
    public TemplateValueGatherType? GatherType { get; set; }

    public object? Configuration { get; set; }
}


public class DtoTemplateValueConfigurationBaseValidator : AbstractValidator<DtoTemplateValueConfigurationBase>
{
    public DtoTemplateValueConfigurationBaseValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.GatherType).NotEmpty();
    }
}
