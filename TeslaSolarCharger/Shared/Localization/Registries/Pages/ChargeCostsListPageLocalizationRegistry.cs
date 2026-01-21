using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class ChargeCostsListPageLocalizationRegistry : TextLocalizationRegistry<ChargeCostsListPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargeCostsListTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register(TranslationKeys.ChargeCostsListNewButton,
            new TextLocalizationTranslation(LanguageCodes.English, "New"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neu"));

        Register(TranslationKeys.ChargeCostsListIdHeader,
            new TextLocalizationTranslation(LanguageCodes.English, "Id"),
            new TextLocalizationTranslation(LanguageCodes.German, "ID"));

        Register(TranslationKeys.ChargeCostsListValidSinceHeader,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid since"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig seit"));

        Register(TranslationKeys.ChargeCostsListSolarPriceHeader,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarpreis pro kWh"));

        Register(TranslationKeys.ChargeCostsListGridPriceHeader,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis pro kWh"));

        Register(TranslationKeys.ChargeCostsListDeleteHeader,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen"));

        Register(TranslationKeys.ChargeCostsListLoading,
            new TextLocalizationTranslation(LanguageCodes.English, "Loading..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden..."));
    }
}
