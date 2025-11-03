using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class InstallationInformationLocalizationRegistry : TextLocalizationRegistry<InstallationInformationLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ServerTimezoneTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "This is needed to properly start charging sessions. If this timezone does not match your own timezone, check the set timezone in your docker-compose.yml"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dies wird benötigt, um Ladesitzungen korrekt zu starten. Wenn diese Zeitzone nicht Ihrer eigenen entspricht, überprüfen Sie die in der docker-compose.yml eingestellte Zeitzone."));

        Register(TranslationKeys.ServerTimeTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "This is needed to properly start charging sessions. If this time does not match your current time, check your server time."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dies wird benötigt, um Ladesitzungen korrekt zu starten. Wenn diese Zeit nicht mit Ihrer aktuellen Zeit übereinstimmt, überprüfen Sie die Serverzeit."));

        Register(TranslationKeys.ServerTimezone,
            new TextLocalizationTranslation(LanguageCodes.English, "Server Timezone"),
            new TextLocalizationTranslation(LanguageCodes.German, "Server-Zeitzone"));

        Register(TranslationKeys.CurrentServerTime,
            new TextLocalizationTranslation(LanguageCodes.English, "Current Server Time"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktuelle Serverzeit"));

        Register(TranslationKeys.CouldNotLoadVersion,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load version"),
            new TextLocalizationTranslation(LanguageCodes.German, "Version konnte nicht geladen werden"));

        Register(TranslationKeys.Version,
            new TextLocalizationTranslation(LanguageCodes.English, "Version"),
            new TextLocalizationTranslation(LanguageCodes.German, "Version"));

        Register(TranslationKeys.InstallationId,
            new TextLocalizationTranslation(LanguageCodes.English, "Installation ID"),
            new TextLocalizationTranslation(LanguageCodes.German, "Installations-ID"));

        Register(TranslationKeys.DoNotShareTheCompleteId,
            new TextLocalizationTranslation(LanguageCodes.English, "Do not share the complete ID with anyone"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie die vollständige ID nicht an andere weiter"));

        Register(TranslationKeys.LanguageSettings,
            new TextLocalizationTranslation(LanguageCodes.English, "Language settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spracheinstellungen"));

        Register(TranslationKeys.DoNotShareTheIdWith,
            new TextLocalizationTranslation(LanguageCodes.English, "Do not share the ID with anyone"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie die ID nicht an andere weiter"));
    }
}
