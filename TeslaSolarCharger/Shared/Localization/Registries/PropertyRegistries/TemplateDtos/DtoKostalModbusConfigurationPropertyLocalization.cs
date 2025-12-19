using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Kostal;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoKostalModbusConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoKostalModbusConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Host", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Host", null));

        Register(x => x.Port,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Port", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Port", null));

        Register(x => x.UnitId,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Unit Identifier", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Einheiten-ID", null));
    }
}
