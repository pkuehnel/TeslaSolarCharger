using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class GenericValueConfigurationComponentLocalizationRegistry : TextLocalizationRegistry<GenericValueConfigurationComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.GenericValueConfigurationSources,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} sources"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} Quellen"));

        Register(TranslationKeys.GenericValueConfigurationRefreshValues,
            new TextLocalizationTranslation(LanguageCodes.English, "Refresh values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Werte aktualisieren"));

        Register(TranslationKeys.GenericValueConfigurationNotAvailable,
            new TextLocalizationTranslation(LanguageCodes.English, "Not available"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht verfügbar"));

        Register(TranslationKeys.GenericValueConfigurationConfigure,
            new TextLocalizationTranslation(LanguageCodes.English, "Configure"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurieren"));

        Register(TranslationKeys.GenericValueConfigurationDelete,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen"));

        Register(TranslationKeys.GenericValueConfigurationAddNewSource,
            new TextLocalizationTranslation(LanguageCodes.English, "Add new {0} source"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neue {0}-Quelle hinzufügen"));

        Register(TranslationKeys.GenericValueConfigurationLastRefreshedFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Last refreshed at {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuletzt aktualisiert um {0}"));

        Register(TranslationKeys.GenericValueConfigurationUnitWatts,
            new TextLocalizationTranslation(LanguageCodes.English, "W"),
            new TextLocalizationTranslation(LanguageCodes.German, "W"));

        Register(TranslationKeys.GenericValueConfigurationUnitPercent,
            new TextLocalizationTranslation(LanguageCodes.English, "%"),
            new TextLocalizationTranslation(LanguageCodes.German, "%"));
    }
}
