using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class HomePageLocalizationRegistry : TextLocalizationRegistry<HomePageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.HomePageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Home"),
            new TextLocalizationTranslation(LanguageCodes.German, "Startseite"));

        Register(TranslationKeys.HomePageNoCarsOrStationsHintStart,
            new TextLocalizationTranslation(LanguageCodes.English, "You have not configured any cars or charging stations yet. Go to "),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie haben noch keine Autos oder Ladestationen konfiguriert. Gehen Sie zu "));

        Register(TranslationKeys.HomePageNoCarsOrStationsHintMiddle,
            new TextLocalizationTranslation(LanguageCodes.English, " to configure your cars or to "),
            new TextLocalizationTranslation(LanguageCodes.German, " um Ihre Autos zu konfigurieren oder zu "));

        Register(TranslationKeys.HomePageNoCarsOrStationsHintEnd,
            new TextLocalizationTranslation(LanguageCodes.English, " to configure your charging stations."),
            new TextLocalizationTranslation(LanguageCodes.German, " um Ihre Ladestationen zu konfigurieren."));

        Register(TranslationKeys.HomePageReferralLinkText,
            new TextLocalizationTranslation(LanguageCodes.English, "Order your Tesla via my referral link to support the project:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bestellen Sie Ihren Tesla über meinen Empfehlungslink, um das Projekt zu unterstützen:"));

        Register(TranslationKeys.HomePagePaypalTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "PayPal - The safer, easier way to pay online!"),
            new TextLocalizationTranslation(LanguageCodes.German, "PayPal - Die sicherere, einfachere Art, online zu bezahlen!"));

        Register(TranslationKeys.HomePagePaypalAltText,
            new TextLocalizationTranslation(LanguageCodes.English, "Donate with PayPal button"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spenden mit PayPal-Button"));
    }
}
