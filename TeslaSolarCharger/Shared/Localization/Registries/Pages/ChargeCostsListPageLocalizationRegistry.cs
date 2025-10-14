using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class ChargeCostsListPageLocalizationRegistry : TextLocalizationRegistry<ChargeCostsListPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Charge Prices",
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register("New",
            new TextLocalizationTranslation(LanguageCodes.English, "New"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neu"));

        Register("Id",
            new TextLocalizationTranslation(LanguageCodes.English, "Id"),
            new TextLocalizationTranslation(LanguageCodes.German, "ID"));

        Register("Valid since",
            new TextLocalizationTranslation(LanguageCodes.English, "Valid since"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig seit"));

        Register("Solar price per kWh",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarpreis pro kWh"));

        Register("Grid price per kWh",
            new TextLocalizationTranslation(LanguageCodes.English, "Grid price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis pro kWh"));

        Register("Delete",
            new TextLocalizationTranslation(LanguageCodes.English, "Delete"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen"));
    }
}
