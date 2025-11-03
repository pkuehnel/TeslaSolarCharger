using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class HiddenErrorsComponentLocalizationRegistry : TextLocalizationRegistry<HiddenErrorsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.HiddenErrors,
            new TextLocalizationTranslation(LanguageCodes.English, "Hidden errors"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verborgene Fehler"));

        Register(TranslationKeys.TheseErrorsAreCurrentlyNotResolved,
            new TextLocalizationTranslation(LanguageCodes.English, "These errors are currently not resolved but hidden."),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Fehler sind aktuell nicht behoben, sondern ausgeblendet."));

        Register(TranslationKeys.TheListIsOnlyUpdatedOnce,
            new TextLocalizationTranslation(LanguageCodes.English, "The list is only updated once per minute"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Liste wird nur einmal pro Minute aktualisiert"));

        Register(TranslationKeys.Key0Occured1TimeS,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} occured {1} time(s)"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} trat {1} Mal auf"));

        Register(TranslationKeys.HiddenReason0,
            new TextLocalizationTranslation(LanguageCodes.English, "Hidden reason: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ausblendungsgrund: {0}"));

        Register(TranslationKeys.NotEnoughOccurrences,
            new TextLocalizationTranslation(LanguageCodes.English, "Not Enough occurrences"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht genügend Vorkommnisse"));

        Register(TranslationKeys.Dismissed,
            new TextLocalizationTranslation(LanguageCodes.English, "Dismissed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ignoriert"));
    }
}
