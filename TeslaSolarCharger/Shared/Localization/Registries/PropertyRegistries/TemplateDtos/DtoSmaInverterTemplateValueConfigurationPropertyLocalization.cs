using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoSmaInverterTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoSmaInverterTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Host",
                "IP address or DNS name of your SMA inverter"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Host",
                "IP-Adresse oder DNS-Name deines SMA Wechselrichters"));
        Register(x => x.Port,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Modbus-Port",
                "Default value is 502 and should not be changed normally"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Modbus-Port",
                "Der Standardwert ist 502 und sollte normalerweise nicht geändert werden"));
        Register(x => x.UnitId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Unit-ID",
                "Default value is 3 and should not be changed normally"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Unit-ID",
                "Der Standardwert ist 3 und sollte normalerweise nicht geändert werden"));

    }
}
