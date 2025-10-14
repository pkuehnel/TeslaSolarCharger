using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class NavMenuComponentLocalizationRegistry : TextLocalizationRegistry<NavMenuComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Tesla Solar Charger",
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Solar Charger"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Solar Charger"));

        Register("Navigation menu",
            new TextLocalizationTranslation(LanguageCodes.English, "Navigation menu"),
            new TextLocalizationTranslation(LanguageCodes.German, "Navigationsmenü"));

        Register("Overview",
            new TextLocalizationTranslation(LanguageCodes.English, "Overview"),
            new TextLocalizationTranslation(LanguageCodes.German, "Übersicht"));

        Register("Charging Stations",
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register("Car Settings",
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

        Register("Charge Prices",
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register("Cloud Connection",
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register("Base Configuration",
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));

        Register("Support",
            new TextLocalizationTranslation(LanguageCodes.English, "Support"),
            new TextLocalizationTranslation(LanguageCodes.German, "Support"));

        Register("Backup and Restore",
            new TextLocalizationTranslation(LanguageCodes.English, "Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sicherung"));
    }
}
