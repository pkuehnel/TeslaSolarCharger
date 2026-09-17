using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Generic;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoGenericRestTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoGenericRestTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.Host,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Host or IP",
                "The hostname or IP address of the device."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Host oder IP",
                "Der Hostname oder die IP-Adresse des Geräts."));

        Register(x => x.Port,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Port",
                "The default value should not be changed normally."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Port",
                "Der Standardwert sollte normalerweise nicht geändert werden."));

        Register(x => x.Username,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Username",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Benutzername",
                null));

        Register(x => x.Password,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Password",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Passwort",
                null));

        Register(x => x.ApiToken,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "API token",
                "Required for battery control. For sonnenBatterie: activate the JSON Write API in the web interface under software integration and enter the generated token here."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "API-Token",
                "Für die Batteriesteuerung erforderlich. Bei der sonnenBatterie: JSON Write API in der Weboberfläche unter Software-Integration aktivieren und das generierte Token hier eintragen."));

        Register(x => x.DeviceId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Device ID",
                "Inverter number, starting at 0."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Geräte-ID",
                "Wechselrichternummer, beginnend bei 0."));

        Register(x => x.EnableHomeBatteryControl,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Enable home battery control",
                "Allows TSC to block discharging and force charging of the home battery."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimspeichersteuerung aktivieren",
                "Erlaubt TSC, das Entladen des Heimspeichers zu blockieren und das Laden zu erzwingen."));

        Register(x => x.MaxBatteryChargePowerW,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max battery charge power",
                "Power in watts used when TSC forces the battery to charge."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Maximale Batterieladeleistung",
                "Leistung in Watt, die verwendet wird, wenn TSC das Laden der Batterie erzwingt."));
    }
}
