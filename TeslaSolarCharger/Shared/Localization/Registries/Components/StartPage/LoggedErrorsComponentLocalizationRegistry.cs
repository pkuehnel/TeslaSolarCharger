using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class LoggedErrorsComponentLocalizationRegistry : TextLocalizationRegistry<LoggedErrorsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.LoggedErrorsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Errors"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler"));

        Register(TranslationKeys.LoggedErrorsUpdateHint,
            new TextLocalizationTranslation(LanguageCodes.English, "The list is only updated once per minute"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Liste wird nur einmal pro Minute aktualisiert"));
    }
}
