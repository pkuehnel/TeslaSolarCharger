using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class HandledChargesListPageLocalizationRegistry : TextLocalizationRegistry<HandledChargesListPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Handled Charges",
            new TextLocalizationTranslation(LanguageCodes.English, "Handled Charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verarbeitete Ladevorgänge"));

        Register("Hide charging processes with known cars",
            new TextLocalizationTranslation(LanguageCodes.English, "Hide charging processes with known cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgänge mit bekannten Fahrzeugen ausblenden"));

        Register("Hide charging processes where the car is known",
            new TextLocalizationTranslation(LanguageCodes.English, "Hide charging processes where the car is known"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgänge ausblenden, bei denen das Fahrzeug bekannt ist"));

        Register("Minimum consumed energy",
            new TextLocalizationTranslation(LanguageCodes.English, "Minimum consumed energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Minimal verbrauchte Energie"));

        Register("Hide charging processes where less energy is consumed",
            new TextLocalizationTranslation(LanguageCodes.English, "Hide charging processes where less energy is consumed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgänge ausblenden, bei denen weniger Energie verbraucht wurde"));
    }
}
