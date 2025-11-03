using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class NavMenuComponentLocalizationRegistry : TextLocalizationRegistry<NavMenuComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.NavMenuAppTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Solar Charger"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Solar Charger"));

        Register(TranslationKeys.NavMenuAriaLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Navigation menu"),
            new TextLocalizationTranslation(LanguageCodes.German, "Navigationsmenü"));

        Register(TranslationKeys.NavMenuOverview,
            new TextLocalizationTranslation(LanguageCodes.English, "Overview"),
            new TextLocalizationTranslation(LanguageCodes.German, "Übersicht"));

        Register(TranslationKeys.NavMenuChargingStations,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.NavMenuCarSettings,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

        Register(TranslationKeys.NavMenuChargePrices,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register(TranslationKeys.NavMenuCloudConnection,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.NavMenuBaseConfiguration,
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));

        Register(TranslationKeys.NavMenuSupport,
            new TextLocalizationTranslation(LanguageCodes.English, "Support"),
            new TextLocalizationTranslation(LanguageCodes.German, "Support"));

        Register(TranslationKeys.NavMenuBackupAndRestore,
            new TextLocalizationTranslation(LanguageCodes.English, "Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sicherung"));
    }
}
