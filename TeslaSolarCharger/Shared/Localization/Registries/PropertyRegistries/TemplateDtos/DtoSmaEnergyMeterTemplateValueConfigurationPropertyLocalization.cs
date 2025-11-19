using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoSmaEnergyMeterTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoSmaEnergyMeterTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.SerialNumber,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Serial number",
                "Serialnumber of your Energy Meter or Home Manager 2.0. Can be left empty if you only have one Energy Meter (which is the case for most setups)"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Seriennummer",
                "Seriennummer des Energy Meter oder Home Manager 2.0. Kann leer gelassen werden, wenn du nur ein Energy Meter besitzt (Standardfall in den meisten Anwendungen)"));
    }

}
