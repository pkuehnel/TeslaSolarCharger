using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class NavMenuComponentLocalizationRegistry : TextLocalizationRegistry<NavMenuComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.TeslaSolarCharger,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Solar Charger"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Solar Charger"));

        Register(TranslationKeys.NavigationMenu,
            new TextLocalizationTranslation(LanguageCodes.English, "Navigation menu"),
            new TextLocalizationTranslation(LanguageCodes.German, "Navigationsmenü"));

        Register(TranslationKeys.Overview,
            new TextLocalizationTranslation(LanguageCodes.English, "Overview"),
            new TextLocalizationTranslation(LanguageCodes.German, "Übersicht"));

        Register(TranslationKeys.SharedChargingStationsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.SharedCarSettingsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

        Register(TranslationKeys.ChargePrices,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register(TranslationKeys.SharedCloudConnectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.SharedBaseConfigurationTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));

        Register(TranslationKeys.Support,
            new TextLocalizationTranslation(LanguageCodes.English, "Support"),
            new TextLocalizationTranslation(LanguageCodes.German, "Support"));

        Register(TranslationKeys.BackupAndRestore,
            new TextLocalizationTranslation(LanguageCodes.English, "Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sicherung"));
    }
}
