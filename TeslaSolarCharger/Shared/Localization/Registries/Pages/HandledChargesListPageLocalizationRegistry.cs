using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class HandledChargesListPageLocalizationRegistry : TextLocalizationRegistry<HandledChargesListPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.HandledChargesListTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Handled Charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verarbeitete Ladevorgänge"));

        Register(TranslationKeys.HandledChargesListHideKnownCarsLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Hide charging processes with known cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgänge mit bekannten Fahrzeugen ausblenden"));

        Register(TranslationKeys.HandledChargesListMinConsumedEnergyLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Minimum consumed energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Minimal verbrauchte Energie"));

        Register(TranslationKeys.HandledChargesListMinConsumedEnergyHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Hide charging processes where less energy is consumed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgänge ausblenden, bei denen weniger Energie verbraucht wurde"));
    }
}
