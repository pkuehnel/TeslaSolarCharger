using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class ChargeCostDetailPageLocalizationRegistry : TextLocalizationRegistry<ChargeCostDetailPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargeCostDetailAllChargeCostsButton,
            new TextLocalizationTranslation(LanguageCodes.English, "All Charge costs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Alle Ladekosten"));

        Register(TranslationKeys.ChargeCostDetailTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "ChargePriceDetail"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreisdetails"));

        Register(TranslationKeys.ChargeCostDetailIdLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "ID"),
            new TextLocalizationTranslation(LanguageCodes.German, "ID"));

        Register(TranslationKeys.ChargeCostDetailValidSinceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid Since"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig seit"));

        Register(TranslationKeys.ChargeCostDetailSolarPriceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarpreis"));

        Register(TranslationKeys.ChargeCostDetailEnergyProviderLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Energy Provider"),
            new TextLocalizationTranslation(LanguageCodes.German, "Energieanbieter"));

        Register(TranslationKeys.ChargeCostDetailFixedPriceOrSpotPriceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Fixed Price or Spot price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Festpreis oder Spotpreis"));

        Register(TranslationKeys.ChargeCostDetailTimeBasedPricesLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Time based prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zeitabhängige Preise"));

        Register(TranslationKeys.ChargeCostDetailBasePriceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Base Price (Spot price will be added to this price)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Grundpreis (Spotpreis wird zu diesem Preis addiert)"));

        Register(TranslationKeys.ChargeCostDetailGridPriceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis"));

        Register(TranslationKeys.ChargeCostDetailTimeBasedPriceHint,
            new TextLocalizationTranslation(LanguageCodes.English, "You can specify times with special prices here. If there are times left, you didn't specify a price for, the default grid price, specified above, is used."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hier kannst du Zeiten mit speziellen Preisen festlegen. Für verbleibende Zeiten ohne Preisangabe wird der oben angegebene Standardnetzpreis verwendet."));

        Register(TranslationKeys.ChargeCostDetailPriceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Preis"));

        Register(TranslationKeys.ChargeCostDetailAddTimeBasedPriceButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add time based price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zeitabhängigen Preis hinzufügen"));

        Register(TranslationKeys.ChargeCostDetailSpotPriceRegionLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Spot Price Region"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spotpreis-Region"));

        Register(TranslationKeys.ChargeCostDetailUseSpotPricesLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Use Spot Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spotpreise verwenden"));

        Register(TranslationKeys.ChargeCostDetailUseSpotPricesHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Enable this if you are using dynamic prices based on EPEX Spot DE (e.g. Tibber or aWATTar)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktiviere dies, wenn du dynamische Preise basierend auf EPEX Spot DE verwendest (z. B. Tibber oder aWATTar)"));

        Register(TranslationKeys.ChargeCostDetailAdditionalCostsToSpotPriceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Additional costs to spotprice"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zusätzliche Kosten zum Spotpreis"));

        Register(TranslationKeys.ChargeCostDetailSurchargeToSpotPriceHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Surcharge to spot price (e.g. aWATTar 3% + 19% VAT in Germany). Note: Spot prices are without VAT."),
            new TextLocalizationTranslation(LanguageCodes.German, "Aufschlag auf den Spotpreis (z. B. aWATTar 3 % + 19 % MwSt. in Deutschland). Hinweis: Spotpreise sind ohne MwSt."));

        Register(TranslationKeys.ChargeCostDetailUpdateWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Updating charge prices can take a significant amount of time as the prices of all previous charges are updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Aktualisieren der Ladepreise kann einige Zeit dauern, da die Preise aller bisherigen Ladevorgänge angepasst werden."));

        Register(TranslationKeys.ChargeCostDetailSaveError,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge price is null and can not be saved. Try reloading the page."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Ladepreis ist leer und kann nicht gespeichert werden. Versuche, die Seite neu zu laden."));
    }
}
