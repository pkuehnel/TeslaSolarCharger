using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoSmaEnergyMeterTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoSmaEnergyMeterTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.SerialNumber,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Serial Number", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Seriennummer", null));
    }
}
