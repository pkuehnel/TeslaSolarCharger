using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class ChargeCostDetailPageLocalizationRegistry : TextLocalizationRegistry<ChargeCostDetailPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("All Charge costs",
            new TextLocalizationTranslation(LanguageCodes.English, "All Charge costs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Alle Ladekosten"));

        Register("ChargePriceDetail",
            new TextLocalizationTranslation(LanguageCodes.English, "ChargePriceDetail"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreisdetails"));

        Register("ID",
            new TextLocalizationTranslation(LanguageCodes.English, "ID"),
            new TextLocalizationTranslation(LanguageCodes.German, "ID"));

        Register("Valid Since",
            new TextLocalizationTranslation(LanguageCodes.English, "Valid Since"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig seit"));

        Register("Solar Price",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarpreis"));

        Register("Energy Provider",
            new TextLocalizationTranslation(LanguageCodes.English, "Energy Provider"),
            new TextLocalizationTranslation(LanguageCodes.German, "Energieanbieter"));

        Register("Fixed Price or Spot price",
            new TextLocalizationTranslation(LanguageCodes.English, "Fixed Price or Spot price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Festpreis oder Spotpreis"));

        Register("Time based prices",
            new TextLocalizationTranslation(LanguageCodes.English, "Time based prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zeitabhängige Preise"));

        Register("Base Price (Spot price will be added to this price)",
            new TextLocalizationTranslation(LanguageCodes.English, "Base Price (Spot price will be added to this price)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Grundpreis (Spotpreis wird zu diesem Preis addiert)"));

        Register("Grid Price",
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis"));

        Register("You can specify times with special prices here. If there are times left, you didn't specify a price for, the default grid price, specified above, is used.",
            new TextLocalizationTranslation(LanguageCodes.English, "You can specify times with special prices here. If there are times left, you didn't specify a price for, the default grid price, specified above, is used."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hier kannst du Zeiten mit speziellen Preisen festlegen. Für verbleibende Zeiten ohne Preisangabe wird der oben angegebene Standardnetzpreis verwendet."));

        Register("Price",
            new TextLocalizationTranslation(LanguageCodes.English, "Price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Preis"));

        Register("Add time based price",
            new TextLocalizationTranslation(LanguageCodes.English, "Add time based price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zeitabhängigen Preis hinzufügen"));

        Register("Use Spot Prices",
            new TextLocalizationTranslation(LanguageCodes.English, "Use Spot Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spotpreise verwenden"));

        Register("Enable this if you are using dynamic prices based on EPEX Spot DE (e.g. Tibber or aWATTar)",
            new TextLocalizationTranslation(LanguageCodes.English, "Enable this if you are using dynamic prices based on EPEX Spot DE (e.g. Tibber or aWATTar)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktiviere dies, wenn du dynamische Preise basierend auf EPEX Spot DE verwendest (z. B. Tibber oder aWATTar)"));

        Register("Additional costs to spotprice",
            new TextLocalizationTranslation(LanguageCodes.English, "Additional costs to spotprice"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zusätzliche Kosten zum Spotpreis"));

        Register("Surcharge to spot price (e.g. aWATTar 3% + 19% VAT in Germany). Note: Spot prices are without VAT.",
            new TextLocalizationTranslation(LanguageCodes.English, "Surcharge to spot price (e.g. aWATTar 3% + 19% VAT in Germany). Note: Spot prices are without VAT."),
            new TextLocalizationTranslation(LanguageCodes.German, "Aufschlag auf den Spotpreis (z. B. aWATTar 3 % + 19 % MwSt. in Deutschland). Hinweis: Spotpreise sind ohne MwSt."));

        Register("Updating charge prices can take a significant amount of time as the prices of all previous charges are updated",
            new TextLocalizationTranslation(LanguageCodes.English, "Updating charge prices can take a significant amount of time as the prices of all previous charges are updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Aktualisieren der Ladepreise kann einige Zeit dauern, da die Preise aller bisherigen Ladevorgänge angepasst werden."));
    }
}
