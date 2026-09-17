using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.TeslaPowerwall;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;

public class DtoTeslaPowerwallTemplateValueConfigurationPropertyLocalization : PropertyLocalizationRegistry<DtoTeslaPowerwallTemplateValueConfiguration>
{
    protected override void Configure()
    {
        Register(x => x.EnergySiteId,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Powerwall Site",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Powerwall Standort",
                null));

        Register(x => x.EnableHomeBatteryControl,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Enable home battery control",
                "Allows TSC to block discharging and force charging of the Powerwall by adjusting its backup reserve."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimspeichersteuerung aktivieren",
                "Erlaubt TSC, das Entladen der Powerwall zu blockieren und das Laden zu erzwingen, indem die Notstromreserve angepasst wird."));

        Register(x => x.NormalModeBackupReservePercent,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Default backup reserve",
                "Backup reserve in percent that is restored when TSC does not force a battery mode. Should match the backup reserve configured in the Tesla app."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Standard-Notstromreserve",
                "Notstromreserve in Prozent, die wiederhergestellt wird, wenn TSC keinen Batteriemodus erzwingt. Sollte der in der Tesla-App konfigurierten Notstromreserve entsprechen."));
    }

}
