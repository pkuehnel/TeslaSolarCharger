using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class ChargePricePropertyLocalization : PropertyLocalizationRegistry<DtoChargePrice>
{
    protected override void Configure()
    {
        Register(x => x.Id,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "ID",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "ID",
                null));

        Register(x => x.ValidSince,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Valid Since",
                "Date from which this charge price configuration becomes effective."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Gültig seit",
                "Datum, ab dem diese Ladepreiskonfiguration gilt."));

        Register(x => x.SolarPrice,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Solar Price",
                "Price per kWh when the vehicle is charged with solar energy."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Solarpreis",
                "Preis pro kWh, wenn das Fahrzeug mit Solarenergie geladen wird."));

        Register(x => x.GridPrice,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Grid Price",
                "Base price per kWh that is used when charging from the grid or when no time based price is defined."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Netzpreis",
                "Grundpreis pro kWh, der beim Laden aus dem Netz oder ohne zeitabhängigen Preis verwendet wird."));

        Register(x => x.AddSpotPriceToGridPrice,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Use Spot Prices",
                "Enable this to add the market spot price of your region to the base grid price."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Spotpreise verwenden",
                "Aktiviere dies, um den Spotmarktpreis deiner Region zum Basis-Netzpreis hinzuzufügen."));

        Register(x => x.SpotPriceRegion,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Spot Price Region",
                "Region that should be used when retrieving spot market prices."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Spotpreis-Region",
                "Region, die beim Abrufen der Spotmarktpreise verwendet werden soll."));

        Register(x => x.SpotPriceSurcharge,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Additional costs to spot price",
                "Percentage that is added to the raw spot price (e.g. provider fee or taxes)."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Zusätzliche Kosten zum Spotpreis",
                "Prozentsatz, der zum Spotpreis hinzugerechnet wird (z. B. Anbietergebühr oder Steuern)."));
    }
}

