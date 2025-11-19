using FluentValidation;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper;

namespace TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

public class DtoTemplateValueConfigurationBase
{
    public  int Id { get; set; }
    public string? Name { get; set; }
    public int? MinRefreshIntervalMilliseconds { get; set; }
    public TemplateValueGatherType? GatherType { get; set; }

    public JObject? Configuration { get; set; }
}


public class DtoTemplateValueConfigurationBaseValidator : AbstractValidator<DtoTemplateValueConfigurationBase>
{
    public DtoTemplateValueConfigurationBaseValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.GatherType).NotEmpty();

        RuleFor(x => x)
            .Custom((dto, context) =>
            {
                if (dto.GatherType == default)
                {
                    // Nothing to validate here; NotEmpty() on GatherType will handle this.
                    return;
                }

                var configType =
                    TemplateValueConfigurationTypeHelper.GetConfigurationType(dto.GatherType.Value);

                if (configType is null)
                {
                    context.AddFailure(nameof(dto.GatherType),
                        $"No configuration type registered for gather type '{dto.GatherType}'.");
                    return;
                }

                if (dto.Configuration is null)
                {
                    context.AddFailure(nameof(dto.Configuration),
                        $"Configuration is required for gather type '{dto.GatherType}'.");
                    return;
                }

                try
                {
                    // This will throw if the JSON can't be deserialized to the expected type
                    _ = dto.Configuration.ToObject(configType);
                }
                catch (Exception ex)
                {
                    context.AddFailure(nameof(dto.Configuration),
                        $"Configuration does not match expected type '{configType.Name}' " +
                        $"for gather type '{dto.GatherType}': {ex.Message}");
                }
            });
    }
}
