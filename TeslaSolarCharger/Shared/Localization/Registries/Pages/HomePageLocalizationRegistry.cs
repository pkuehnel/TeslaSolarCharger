using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class HomePageLocalizationRegistry : TextLocalizationRegistry<HomePageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Solar 4 Car",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar 4 Car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar 4 Car"));

        Register("No cars and no charging stations should be managed. Check out",
            new TextLocalizationTranslation(LanguageCodes.English, "No cars and no charging stations should be managed. Check out"),
            new TextLocalizationTranslation(LanguageCodes.German, "Es sollten keine Autos und keine Ladestationen verwaltet werden. Schau bei"));

        Register("to add a car or",
            new TextLocalizationTranslation(LanguageCodes.English, "to add a car or"),
            new TextLocalizationTranslation(LanguageCodes.German, "vorbei, um ein Auto hinzuzufügen oder bei"));

        Register("to add a charging station.",
            new TextLocalizationTranslation(LanguageCodes.English, "to add a charging station."),
            new TextLocalizationTranslation(LanguageCodes.German, "um eine Ladestation hinzuzufügen."));

        Register("Use my referral code for ordering any Tesla product or schedule a Demo Drive:",
            new TextLocalizationTranslation(LanguageCodes.English, "Use my referral code for ordering any Tesla product or schedule a Demo Drive:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nutze meinen Empfehlungslink, um ein beliebiges Tesla-Produkt zu bestellen oder eine Probefahrt zu vereinbaren:"));

        Register("PayPal - The safer, easier way to pay online!",
            new TextLocalizationTranslation(LanguageCodes.English, "PayPal - The safer, easier way to pay online!"),
            new TextLocalizationTranslation(LanguageCodes.German, "PayPal – Die sichere und einfache Art, online zu bezahlen!"));

        Register("Donate with PayPal button",
            new TextLocalizationTranslation(LanguageCodes.English, "Donate with PayPal button"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spenden mit PayPal-Schaltfläche"));
    }
}
