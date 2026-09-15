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
            new TextLocalizationTranslation(LanguageCodes.German, "Sie haben noch keine Fahrzeuge oder Ladestationen konfiguriert. Gehen Sie zu "));

        Register(TranslationKeys.HomePageNoCarsOrStationsHintMiddle,
            new TextLocalizationTranslation(LanguageCodes.English, " to configure your cars or to "),
            new TextLocalizationTranslation(LanguageCodes.German, " um Ihre Fahrzeuge zu konfigurieren, oder zu "));

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

        Register(TranslationKeys.SignalRReconnecting,
            new TextLocalizationTranslation(LanguageCodes.English, "Not connected. The system is automatically trying to reconnect..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Verbindung. Das System versucht automatisch die Verbindung wiederherzustellen..."));

        Register(TranslationKeys.SignalRReconnectionDelayHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Possible causes: poor WiFi connection, or device is outside of home network and cannot reconnect."),
            new TextLocalizationTranslation(LanguageCodes.German, "Mögliche Ursachen: schwache WLAN-Verbindung oder das Gerät befindet sich außerhalb des Heimnetzwerks und kann keine Verbindung herstellen."));

        Register(TranslationKeys.SignalRConnected,
            new TextLocalizationTranslation(LanguageCodes.English, "Connection established."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindung wiederhergestellt."));

        Register(TranslationKeys.PendingChecksTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Not tried in the real world yet"),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch nicht in der Praxis erprobt"));

        Register(TranslationKeys.PendingChecksExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Everything is set up. We just have not seen this actually charge yet. Next time it charges at home, this disappears on its own - there is nothing for you to do."),
            new TextLocalizationTranslation(LanguageCodes.German, "Es ist alles eingerichtet. Wir haben nur noch nicht gesehen, dass hier wirklich geladen wird. Beim nächsten Laden zu Hause verschwindet dieser Hinweis von selbst – Sie müssen nichts tun."));

        Register(TranslationKeys.PendingChecksItemFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} has not charged under our control yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} hat noch nicht unter unserer Steuerung geladen."));

        Register(TranslationKeys.PendingChecksUnnamedDevice,
            new TextLocalizationTranslation(LanguageCodes.English, "This device"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Gerät"));

        Register(TranslationKeys.PendingChecksLastAttemptFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "last attempt did not work"),
            new TextLocalizationTranslation(LanguageCodes.German, "letzter Versuch hat nicht funktioniert"));

        Register(TranslationKeys.PendingChecksDismissButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Hide this"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ausblenden"));
    }
}
