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

        Register(TranslationKeys.HandledChargesListStartTime,
            new TextLocalizationTranslation(LanguageCodes.English, "Start Time"),
            new TextLocalizationTranslation(LanguageCodes.German, "Startzeit"));

        Register(TranslationKeys.HandledChargesListEndTime,
            new TextLocalizationTranslation(LanguageCodes.English, "End Time"),
            new TextLocalizationTranslation(LanguageCodes.German, "Endzeit"));

        Register(TranslationKeys.HandledChargesListCalculatedPrice,
            new TextLocalizationTranslation(LanguageCodes.English, "Calculated Price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Berechneter Preis"));

        Register(TranslationKeys.HandledChargesListPricePerKwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Preis pro kWh"));

        Register(TranslationKeys.HandledChargesListUsedGridEnergy,
            new TextLocalizationTranslation(LanguageCodes.English, "Used Grid Energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Genutzte Netzenergie"));

        Register(TranslationKeys.HandledChargesListUsedHomeBatteryEnergy,
            new TextLocalizationTranslation(LanguageCodes.English, "Used Home Battery Energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Genutzte Heimbatterie-Energie"));

        Register(TranslationKeys.HandledChargesListUsedSolarEnergy,
            new TextLocalizationTranslation(LanguageCodes.English, "Used Solar Energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Genutzte Solarenergie"));

        Register(TranslationKeys.HandledChargesListSumFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "\u2211: {value}"),
            new TextLocalizationTranslation(LanguageCodes.German, "\u2211: {value}"));

        Register(TranslationKeys.HandledChargesListAverageFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "\u2300: {0:F3}"),
            new TextLocalizationTranslation(LanguageCodes.German, "\u2300: {0:F3}"));
    }
}
