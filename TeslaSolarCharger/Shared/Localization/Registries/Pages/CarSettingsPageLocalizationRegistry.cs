using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class CarSettingsPageLocalizationRegistry : TextLocalizationRegistry<CarSettingsPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarSettingsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

        Register(TranslationKeys.CarSettingsCreateTokenTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Fleet API Token is not valid."),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Fleet API Token ist ungültig."));

        Register(TranslationKeys.CarSettingsGoToPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "Go to "),
            new TextLocalizationTranslation(LanguageCodes.German, "Gehe zu "));

        Register(TranslationKeys.CarSettingsGenerateTokenSuffix,
            new TextLocalizationTranslation(LanguageCodes.English, " and generate a Tesla Fleet API Token."),
            new TextLocalizationTranslation(LanguageCodes.German, " und generiere ein Tesla Fleet API Token."));

        Register(TranslationKeys.CarSettingsRestartTscTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Restart TSC"),
            new TextLocalizationTranslation(LanguageCodes.German, "TSC neu starten"));

        Register(TranslationKeys.CarSettingsRestartTscHint,
            new TextLocalizationTranslation(LanguageCodes.English, "The Fleet API token has been updated. Please restart the TSC container to apply the changes."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Fleet-API-Token wurde aktualisiert. Bitte starten Sie den TSC-Container neu, um die Änderungen zu übernehmen."));

        Register(TranslationKeys.CarSettingsAddNonTeslaButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add non Tesla"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht-Tesla hinzufügen"));

        Register(TranslationKeys.CarSettingsCurrentBelow6AWarningTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Current below 6A not recommended"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke unter 6A nicht empfohlen"));

        Register(TranslationKeys.CarSettingsCurrentBelow6AWarningContent,
            new TextLocalizationTranslation(LanguageCodes.English, "The Type 2 standard states that the minimum current below 6A is not allowed. Setting this below 6A might result in unexpected behavior like the car not charging at all."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Typ-2-Standard besagt, dass ein Mindeststrom unter 6A nicht zulässig ist. Wenn Sie diesen Wert unter 6A einstellen, kann dies zu unerwartetem Verhalten führen, z. B. dass das Auto gar nicht lädt."));

        Register(TranslationKeys.CarSettingsGpsHomeDetectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "GPS Home Detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "GPS-Heimerkennung"));

        Register(TranslationKeys.CarSettingsGpsHomeDetectionHintStart,
            new TextLocalizationTranslation(LanguageCodes.English, "Home is detected via GPS coordinates. Make sure the coordinates are set correctly in "),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause wird über GPS-Koordinaten erkannt. Stellen Sie sicher, dass die Koordinaten in "));

        Register(TranslationKeys.CarSettingsGpsHomeDetectionHintEnd,
            new TextLocalizationTranslation(LanguageCodes.English, "."),
            new TextLocalizationTranslation(LanguageCodes.German, "."));

        Register(TranslationKeys.CarSettingsTeslaNavHomeDetectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Nav Home Detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Navi-Heimerkennung"));

        Register(TranslationKeys.CarSettingsTeslaNavHomeDetectionHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Home is detected if the car reports to be at 'Home'."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause wird erkannt, wenn das Auto meldet, 'Zuhause' zu sein."));

        Register(TranslationKeys.CarSettingsTeslaNavWorkDetectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Nav Work Detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Navi-Arbeitserkennung"));

        Register(TranslationKeys.CarSettingsTeslaNavWorkDetectionHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Home is detected if the car reports to be at 'Work'."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause wird erkannt, wenn das Auto meldet, bei der 'Arbeit' zu sein."));

        Register(TranslationKeys.CarSettingsTeslaNavFavoriteDetectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Nav Favorite Detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Navi-Favoritenerkennung"));

        Register(TranslationKeys.CarSettingsTeslaNavFavoriteDetectionHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Home is detected if the car reports to be at a 'Favorite' location. Note: This might include multiple locations."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause wird erkannt, wenn das Auto meldet, an einem 'Favoriten'-Ort zu sein. Hinweis: Dies kann mehrere Orte umfassen."));

        Register(TranslationKeys.CarSettingsHomeDetectionViaLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Detection via"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimerkennung über"));

        Register(TranslationKeys.CarSettingsBlePairingTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "BLE Pairing"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Kopplung"));

        Register(TranslationKeys.CarSettingsBlePairingHintStart,
            new TextLocalizationTranslation(LanguageCodes.English, "To use BLE commands you need to pair the TSC with your car. See "),
            new TextLocalizationTranslation(LanguageCodes.German, "Um BLE-Befehle nutzen zu können, müssen Sie TSC mit Ihrem Auto koppeln. Siehe "));

        Register(TranslationKeys.CarSettingsBlePairingLinkText,
            new TextLocalizationTranslation(LanguageCodes.English, "documentation"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dokumentation"));

        Register(TranslationKeys.CarSettingsBlePairingHintEnd,
            new TextLocalizationTranslation(LanguageCodes.English, " for more details."),
            new TextLocalizationTranslation(LanguageCodes.German, " für weitere Details."));

        Register(TranslationKeys.CarSettingsBlePairingNote,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: You need to be close to the car with your phone/key card to approve the pairing request."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Sie müssen sich mit Ihrem Telefon/Ihrer Schlüsselkarte in der Nähe des Autos befinden, um die Kopplungsanfrage zu genehmigen."));

        Register(TranslationKeys.CarSettingsBlePairButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Pair Car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto koppeln"));

        Register(TranslationKeys.CarSettingsTestBleAccessTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Test BLE Access"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Zugriff testen"));

        Register(TranslationKeys.CarSettingsTestBleAccessHint1,
            new TextLocalizationTranslation(LanguageCodes.English, "Click the button below to test if TSC can send commands via BLE."),
            new TextLocalizationTranslation(LanguageCodes.German, "Klicken Sie auf die Schaltfläche unten, um zu testen, ob TSC Befehle über BLE senden kann."));

        Register(TranslationKeys.CarSettingsTestBleAccessHint2,
            new TextLocalizationTranslation(LanguageCodes.English, "This will try to set the charging amps to 7A."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dies versucht, den Ladestrom auf 7A einzustellen."));

        Register(TranslationKeys.CarSettingsSetTo7AButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set to 7A"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auf 7A setzen"));

        Register(TranslationKeys.CarSettingsTestWakeupTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Test Wake Up"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aufwecken testen"));

        Register(TranslationKeys.CarSettingsTestWakeupHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Click the button below to test if TSC can wake up the car via BLE."),
            new TextLocalizationTranslation(LanguageCodes.German, "Klicken Sie auf die Schaltfläche unten, um zu testen, ob TSC das Auto über BLE aufwecken kann."));

        Register(TranslationKeys.CarSettingsWakeUpButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Wake Up"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aufwecken"));

        Register(TranslationKeys.CarSettingsDeserializationError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not deserialize result"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebnis konnte nicht deserialisiert werden"));

        Register(TranslationKeys.CarSettingsBleSuccessMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Command successfully sent via BLE"),
            new TextLocalizationTranslation(LanguageCodes.German, "Befehl erfolgreich über BLE gesendet"));

        Register(TranslationKeys.CarSettingsWakeUpSuccessMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Wake up command successfully sent via BLE"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aufweckbefehl erfolgreich über BLE gesendet"));

        Register(TranslationKeys.AddCarDialogTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Add car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto hinzufügen"));

        Register(TranslationKeys.AddCarTokenInvalidTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Fleet API Token is not valid."),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Fleet API Token ist ungültig."));

        Register(TranslationKeys.AddCarTokenInvalidContent,
            new TextLocalizationTranslation(LanguageCodes.English, "Go to "),
            new TextLocalizationTranslation(LanguageCodes.German, "Gehe zu "));

        Register(TranslationKeys.AddCarCloudConnectionLink,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.HomeDetectionViaGpsLocation,
            new TextLocalizationTranslation(LanguageCodes.English, "GPS Location"),
            new TextLocalizationTranslation(LanguageCodes.German, "GPS-Standort"));

        Register(TranslationKeys.HomeDetectionViaLocatedAtHome,
            new TextLocalizationTranslation(LanguageCodes.English, "At Home"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause"));

        Register(TranslationKeys.HomeDetectionViaLocatedAtWork,
            new TextLocalizationTranslation(LanguageCodes.English, "At Work"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bei der Arbeit"));

        Register(TranslationKeys.HomeDetectionViaLocatedAtFavorite,
            new TextLocalizationTranslation(LanguageCodes.English, "At Favorite"),
            new TextLocalizationTranslation(LanguageCodes.German, "An Favoriten"));
    }
}
