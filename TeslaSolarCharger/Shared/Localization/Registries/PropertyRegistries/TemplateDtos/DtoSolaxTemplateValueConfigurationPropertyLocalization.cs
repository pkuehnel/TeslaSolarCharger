using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Solax;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoSolaxTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoSolaxTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Host",
                "IP address or DNS name of your Solax system"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Host",
                "IP-Adresse oder DNS-Name deines Solax systems"));

        Register(x => x.Password,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Password",
                "Password of your solar system (default is your wifi dongle serial number)"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Passwort",
                "Passwort deines PV-Systems (Standardwert ist die Seriennummer deines Wifi Dongles)"));
    }
}
