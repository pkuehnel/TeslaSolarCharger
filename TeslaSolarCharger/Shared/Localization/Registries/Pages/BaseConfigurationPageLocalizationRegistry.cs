using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class BaseConfigurationPageLocalizationRegistry : TextLocalizationRegistry<BaseConfigurationPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.BaseConfigurationPageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));

        Register(TranslationKeys.BaseConfigurationGeneralSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "General:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Allgemein:"));

        Register(TranslationKeys.BaseConfigurationTeslaMateSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "TeslaMate:"),
            new TextLocalizationTranslation(LanguageCodes.German, "TeslaMate:"));

        Register(TranslationKeys.BaseConfigurationHomeGeofenceSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Geofence"),
            new TextLocalizationTranslation(LanguageCodes.German, "Home-Geofence"));

        Register(TranslationKeys.LocationUpdateInfoText,
            new TextLocalizationTranslation(LanguageCodes.English, "To update the location, click the save button on the bottom of the page"),
            new TextLocalizationTranslation(LanguageCodes.German, "Um den Standort zu aktualisieren, klicke auf die Schaltfläche zum Speichern am unteren Rand der Seite"));

        Register(TranslationKeys.BaseConfigurationHomeGeofenceHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Click on the map to select your home geofence. Within that area TSC will regulate the charging power."),
            new TextLocalizationTranslation(LanguageCodes.German, "Klicke auf die Karte, um deinen Home-Geofence auszuwählen. Innerhalb dieses Bereichs reguliert TSC die Ladeleistung."));

        Register(TranslationKeys.BaseConfigurationTelegramSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Telegram:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Telegram:"));

        Register(TranslationKeys.BaseConfigurationHowToSetupTelegramLinkText,
            new TextLocalizationTranslation(LanguageCodes.English, "How to set up Telegram"),
            new TextLocalizationTranslation(LanguageCodes.German, "So richtest du Telegram ein"));

        Register(TranslationKeys.BaseConfigurationTelegramBotNote,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: The Telegram bot for now only sends messages if something is not working. E.g. The car does not respond to commands, solar power values can not be refreshed,..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Der Telegram-Bot sendet aktuell nur Nachrichten, wenn etwas nicht funktioniert. Z. B. wenn das Auto nicht auf Befehle reagiert oder Solarleistungswerte nicht aktualisiert werden können ..."));

        Register(TranslationKeys.BaseConfigurationSendTestMessageButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Send test message"),
            new TextLocalizationTranslation(LanguageCodes.German, "Testnachricht senden"));

        Register(TranslationKeys.BaseConfigurationSaveBeforeTestingTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "You need to save the configuration before testing it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Du musst die Konfiguration speichern, bevor du sie testen kannst."));

        Register(TranslationKeys.BaseConfigurationAdvancedSettingsPanelText,
            new TextLocalizationTranslation(LanguageCodes.English, "Advanced settings. Please only change values here if you know what you are doing."),
            new TextLocalizationTranslation(LanguageCodes.German, "Erweiterte Einstellungen. Ändere Werte hier nur, wenn du weißt, was du tust."));

        Register(TranslationKeys.BaseConfigurationLowIntervalWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Values below 25 seconds are not recommended and might cause performance issues."),
            new TextLocalizationTranslation(LanguageCodes.German, "Werte unter 25 Sekunden werden nicht empfohlen und können zu Leistungsproblemen führen."));

        Register(TranslationKeys.BaseConfigurationSavedNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert."));

        Register(TranslationKeys.BaseConfigurationCouldNotGetResult,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not get result"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebnis konnte nicht abgerufen werden"));
    }
}
