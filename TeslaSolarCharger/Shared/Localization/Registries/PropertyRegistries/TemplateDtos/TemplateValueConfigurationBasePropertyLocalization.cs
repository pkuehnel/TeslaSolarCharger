using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class TemplateValueConfigurationBasePropertyLocalization : PropertyLocalizationRegistry<DtoTemplateValueConfigurationBase>
{
    protected override void Configure()
    {
        Register(x => x.Name,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Name", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Name", null));

        Register(x => x.GatherType,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Type", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Typ", null));
    }
}
