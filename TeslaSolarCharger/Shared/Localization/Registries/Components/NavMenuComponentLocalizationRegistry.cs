namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class NavMenuComponentLocalizationRegistry : TextLocalizationRegistry<NavMenuComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.NavMenuTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "TeslaSolarCharger"),
            new TextLocalizationTranslation(LanguageCodes.German, "TeslaSolarCharger"));

        Register(TranslationKeys.NavMenuToggleLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Navigation menu"),
            new TextLocalizationTranslation(LanguageCodes.German, "Navigationsmenü"));

        Register(TranslationKeys.NavMenuOverview,
            new TextLocalizationTranslation(LanguageCodes.English, "Overview"),
            new TextLocalizationTranslation(LanguageCodes.German, "Übersicht"));

        Register(TranslationKeys.ChargingStationsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.CarsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge"));

        Register(TranslationKeys.ChargeCostsListTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register(TranslationKeys.CloudConnectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.BaseConfigurationPageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));

        Register(TranslationKeys.SupportPageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Support"),
            new TextLocalizationTranslation(LanguageCodes.German, "Support"));

        Register(TranslationKeys.NavMenuBackupAndRestore,
            new TextLocalizationTranslation(LanguageCodes.English, "Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sicherung"));
    }
}
