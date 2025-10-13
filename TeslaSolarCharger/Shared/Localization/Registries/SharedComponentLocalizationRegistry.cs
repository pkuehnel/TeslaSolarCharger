using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class SharedComponentLocalizationRegistry : TextLocalizationRegistry<SharedComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Base Configuration",
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));

        Register("Save",
            new TextLocalizationTranslation(LanguageCodes.English, "Save"),
            new TextLocalizationTranslation(LanguageCodes.German, "Speichern"));

        Register("Saved.",
            new TextLocalizationTranslation(LanguageCodes.English, "Saved."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert."));

        Register("Loading...",
            new TextLocalizationTranslation(LanguageCodes.English, "Loading..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird geladen..."));

        Register("Car Settings",
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

        Register("Charging Stations",
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));
    }
}
