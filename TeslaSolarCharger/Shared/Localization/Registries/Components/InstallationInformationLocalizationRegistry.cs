using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class InstallationInformationLocalizationRegistry : TextLocalizationRegistry<InstallationInformationLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.InstallationInfoServerTimezoneTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "This is needed to properly start charging sessions. If this timezone does not match your own timezone, check the set timezone in your docker-compose.yml"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dies wird benötigt, um Ladesitzungen korrekt zu starten. Wenn diese Zeitzone nicht Ihrer eigenen entspricht, überprüfen Sie die in der docker-compose.yml eingestellte Zeitzone."));

        Register(TranslationKeys.InstallationInfoServerTimeTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "This is needed to properly start charging sessions. If this time does not match your current time, check your server time."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dies wird benötigt, um Ladesitzungen korrekt zu starten. Wenn diese Zeit nicht mit Ihrer aktuellen Zeit übereinstimmt, überprüfen Sie die Serverzeit."));

        Register(TranslationKeys.InstallationInfoServerTimezoneLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Server Timezone"),
            new TextLocalizationTranslation(LanguageCodes.German, "Server-Zeitzone"));

        Register(TranslationKeys.InstallationInfoCurrentServerTimeLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Current Server Time"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktuelle Serverzeit"));

        Register(TranslationKeys.InstallationInfoVersionLoadError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load version"),
            new TextLocalizationTranslation(LanguageCodes.German, "Version konnte nicht geladen werden"));

        Register(TranslationKeys.InstallationInfoVersionLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Version"),
            new TextLocalizationTranslation(LanguageCodes.German, "Version"));

        Register(TranslationKeys.InstallationInfoInstallationIdLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Installation ID"),
            new TextLocalizationTranslation(LanguageCodes.German, "Installations-ID"));

        Register(TranslationKeys.InstallationInfoDoNotShareCompleteIdWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Do not share the complete ID with anyone"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie die vollständige ID nicht an andere weiter"));

        Register(TranslationKeys.InstallationInfoLanguageSettingsLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Language settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spracheinstellungen"));

        Register(TranslationKeys.InstallationInfoDoNotShareIdWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Do not share the ID with anyone"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie die ID nicht an andere weiter"));
    }
}
