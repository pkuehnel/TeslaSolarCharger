using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class HandledChargesListPageLocalizationRegistry : TextLocalizationRegistry<HandledChargesListPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.HandledChargesListPageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Handled Charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verarbeitete Ladevorgänge"));

        Register(TranslationKeys.HandledChargesListPageHideKnownCarsToggle,
            new TextLocalizationTranslation(LanguageCodes.English, "Hide charging processes with known cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgänge mit bekannten Fahrzeugen ausblenden"));

        Register(TranslationKeys.HandledChargesListPageMinimumConsumedEnergyLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Minimum consumed energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Minimal verbrauchte Energie"));

        Register(TranslationKeys.HandledChargesListPageHideBelowThresholdDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Hide charging processes where less energy is consumed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgänge ausblenden, bei denen weniger Energie verbraucht wurde"));
    }
}
