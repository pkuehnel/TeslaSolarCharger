using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Solax;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoSolaxTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoSolaxTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Host", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Host", null));

        Register(x => x.Password,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Password", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Passwort", null));
    }
}
