using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class GenericValueConfigurationComponentLocalizationRegistry : TextLocalizationRegistry<GenericValueConfigurationComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("{0} sources",
            new TextLocalizationTranslation(LanguageCodes.English, "{0} sources"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} Quellen"));

        Register("Refresh values",
            new TextLocalizationTranslation(LanguageCodes.English, "Refresh values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Werte aktualisieren"));

        Register("Not available",
            new TextLocalizationTranslation(LanguageCodes.English, "Not available"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht verfügbar"));

        Register("Configure",
            new TextLocalizationTranslation(LanguageCodes.English, "Configure"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurieren"));

        Register("Delete",
            new TextLocalizationTranslation(LanguageCodes.English, "Delete"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen"));

        Register("Add new {0} source",
            new TextLocalizationTranslation(LanguageCodes.English, "Add new {0} source"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neue {0}-Quelle hinzufügen"));
    }
}
