using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class ChargeCostsListPageLocalizationRegistry : TextLocalizationRegistry<ChargeCostsListPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargePrices,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register(TranslationKeys.New,
            new TextLocalizationTranslation(LanguageCodes.English, "New"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neu"));

        Register(TranslationKeys.Id2,
            new TextLocalizationTranslation(LanguageCodes.English, "Id"),
            new TextLocalizationTranslation(LanguageCodes.German, "ID"));

        Register(TranslationKeys.ValidSince2,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid since"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig seit"));

        Register(TranslationKeys.SolarPricePerKwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarpreis pro kWh"));

        Register(TranslationKeys.GridPricePerKwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis pro kWh"));

        Register(TranslationKeys.Delete,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen"));
    }
}
