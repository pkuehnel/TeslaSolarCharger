using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.TeslaPowerwall;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoTeslaPowerwallTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoTeslaPowerwallTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.EnergySiteId,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Energy Site ID", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Energieanlagen-ID", null));
    }
}
