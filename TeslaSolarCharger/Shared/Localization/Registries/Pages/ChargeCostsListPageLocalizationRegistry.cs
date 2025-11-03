using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class ChargeCostsListPageLocalizationRegistry : TextLocalizationRegistry<ChargeCostsListPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargeCostsListPageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register(TranslationKeys.ChargeCostsListPageNewButton,
            new TextLocalizationTranslation(LanguageCodes.English, "New"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neu"));

        Register(TranslationKeys.ChargeCostsListPageIdColumn,
            new TextLocalizationTranslation(LanguageCodes.English, "Id"),
            new TextLocalizationTranslation(LanguageCodes.German, "ID"));

        Register(TranslationKeys.ChargeCostsListPageValidSinceColumn,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid since"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig seit"));

        Register(TranslationKeys.ChargeCostsListPageSolarPricePerKwhColumn,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarpreis pro kWh"));

        Register(TranslationKeys.ChargeCostsListPageGridPricePerKwhColumn,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis pro kWh"));

        Register(TranslationKeys.ChargeCostsListPageDeleteButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen"));
    }
}
