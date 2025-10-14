using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class LoggedErrorsComponentLocalizationRegistry : TextLocalizationRegistry<LoggedErrorsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Errors",
            new TextLocalizationTranslation(LanguageCodes.English, "Errors"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler"));

        Register("The list is only updated once per minute",
            new TextLocalizationTranslation(LanguageCodes.English, "The list is only updated once per minute"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Liste wird nur einmal pro Minute aktualisiert"));

        Register("{0} occured {1} time(s)",
            new TextLocalizationTranslation(LanguageCodes.English, "{0} occured {1} time(s)"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} trat {1} Mal auf"));
    }
}
