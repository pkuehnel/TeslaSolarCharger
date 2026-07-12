using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Fronius;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoFroniusSolarApiTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoFroniusSolarApiTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Host or IP",
                "The hostname or IP address of the Fronius inverter. The Solar API must be enabled in the inverter settings."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Host oder IP",
                "Der Hostname oder die IP-Adresse des Fronius-Wechselrichters. Die Solar API muss in den Wechselrichtereinstellungen aktiviert sein."));

        Register(x => x.EnableHomeBatteryControl,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Enable home battery control",
                "Allows TSC to block discharging of the home battery via the battery management time of use configuration. Forced charging is not supported. Attention: Existing time of use settings under Energy Management - Battery Management are overwritten."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimspeichersteuerung aktivieren",
                "Erlaubt TSC, das Entladen des Heimspeichers über die zeitabhängige Batteriesteuerung zu blockieren. Erzwungenes Laden wird nicht unterstützt. Achtung: Bestehende Einstellungen unter Energiemanagement - Batteriemanagement werden überschrieben."));

        Register(x => x.Username,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Username",
                "Username for the inverter web interface, usually customer. Only required for battery control."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Benutzername",
                "Benutzername für die Weboberfläche des Wechselrichters, üblicherweise customer. Nur für die Batteriesteuerung erforderlich."));

        Register(x => x.Password,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Password",
                "Password for the inverter web interface. Only required for battery control."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Passwort",
                "Passwort für die Weboberfläche des Wechselrichters. Nur für die Batteriesteuerung erforderlich."));

        Register(x => x.UseApiConfigPath,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Use /api/config path",
                "Enable for firmware versions 1.36.5-1 and newer."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "/api/config-Pfad verwenden",
                "Für Firmware-Versionen ab 1.36.5-1 aktivieren."));
    }
}
