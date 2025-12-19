using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class HiddenErrorsComponentLocalizationRegistry : TextLocalizationRegistry<HiddenErrorsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.HiddenErrorsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Hidden errors"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ausgeblendete Fehler"));

        Register(TranslationKeys.HiddenErrorsDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "These errors are currently not resolved but hidden."),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Fehler sind derzeit nicht behoben, aber ausgeblendet."));

        Register(TranslationKeys.HiddenErrorsUpdateHint,
            new TextLocalizationTranslation(LanguageCodes.English, "The list is only updated once per minute"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Liste wird nur einmal pro Minute aktualisiert"));

        Register(TranslationKeys.HiddenErrorsReasonNotEnoughOccurrences,
            new TextLocalizationTranslation(LanguageCodes.English, "Not Enough occurrences"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht genügend Vorkommnisse"));

        Register(TranslationKeys.HiddenErrorsReasonDismissed,
            new TextLocalizationTranslation(LanguageCodes.English, "Dismissed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verworfen"));

        Register(TranslationKeys.HiddenErrorsOccurrenceCount,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} occured {1} time(s)"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} trat {1} mal auf"));
    }
}
