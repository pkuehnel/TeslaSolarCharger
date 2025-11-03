using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class CarSettingsPageLocalizationRegistry : TextLocalizationRegistry<CarSettingsPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.SharedCarSettingsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

        Register(TranslationKeys.CarSettingsCreateTokenLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Create Token."),
            new TextLocalizationTranslation(LanguageCodes.German, "Token erstellen."));

        Register(TranslationKeys.CarSettingsGoToPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "Go to "),
            new TextLocalizationTranslation(LanguageCodes.German, "Gehe zu "));

        Register(TranslationKeys.CarSettingsGenerateTokenSuffix,
            new TextLocalizationTranslation(LanguageCodes.English, ", Generate a Tesla Fleet API Token and restart TSC to see cars here."),
            new TextLocalizationTranslation(LanguageCodes.German, ", generiere ein Tesla Fleet API Token und starte TSC neu, um hier Fahrzeuge zu sehen."));

        Register(TranslationKeys.SharedCloudConnectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.CarSettingsRestartToAddCars,
            new TextLocalizationTranslation(LanguageCodes.English, "Restart TSC to add new cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Starte TSC neu, um neue Fahrzeuge hinzuzufügen"));

        Register(TranslationKeys.CarSettingsRestartIfCarsMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "If you do not see all cars here that are available in your Tesla account, restart TSC."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wenn du hier nicht alle Fahrzeuge siehst, die in deinem Tesla-Konto verfügbar sind, starte TSC neu."));

        Register(TranslationKeys.CarSettingsAddNonTesla,
            new TextLocalizationTranslation(LanguageCodes.English, "Add car (non Tesla)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug hinzufügen (kein Tesla)"));

        Register(TranslationKeys.CarSettingsAddCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Add car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug hinzufügen"));

        Register(TranslationKeys.CarSettingsTokenInvalid,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Fleet API Token is not valid."),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Fleet API Token ist nicht gültig."));

        Register(TranslationKeys.CarSettingsGenerateTokenInstructionSuffix,
            new TextLocalizationTranslation(LanguageCodes.English, " and generate a Tesla Fleet API Token."),
            new TextLocalizationTranslation(LanguageCodes.German, " und generiere ein Tesla Fleet API Token."));

        Register(TranslationKeys.CarSettingsCurrentBelowSixTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Current below 6A not recommended"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke unter 6A nicht empfohlen"));

        Register(TranslationKeys.CarSettingsCurrentBelowSixDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "The Type 2 standard states that the minimum current below 6A is not allowed. Setting this below 6A might result in unexpected behaviour like the car not charging at all."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Typ-2-Standard erlaubt keinen Mindeststrom unter 6A. Wenn du einen niedrigeren Wert einstellst, kann dies zu unerwartetem Verhalten führen, etwa dass das Fahrzeug gar nicht lädt."));

        Register(TranslationKeys.CarSettingsGpsHomeDetection,
            new TextLocalizationTranslation(LanguageCodes.English, "GPS Location used for home detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "GPS-Position zur Zuhause-Erkennung verwendet"));

        Register(TranslationKeys.CarSettingsGpsHomeDetectionDescriptionPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "TSC will manage charging if the car's GPS Location is within the configured Home Geofence set in "),
            new TextLocalizationTranslation(LanguageCodes.German, "TSC steuert das Laden, wenn sich die GPS-Position des Fahrzeugs innerhalb des konfigurierten Home-Geofence in "));

        Register(TranslationKeys.CarSettingsSentencePeriod,
            new TextLocalizationTranslation(LanguageCodes.English, "."),
            new TextLocalizationTranslation(LanguageCodes.German, "."));

        Register(TranslationKeys.CarSettingsNavigationHomeDetection,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla navigation Home used for home detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Navigation Zuhause wird zur Zuhause-Erkennung verwendet"));

        Register(TranslationKeys.CarSettingsNavigationHomeDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "TSC will manage charging if the car is at the home location set in the Tesla navigation system. Note: Different driver profiles with different home addresses might mess this up. Make sure that the last driver has the correct home address set in the Tesla navigation system."),
            new TextLocalizationTranslation(LanguageCodes.German, "TSC steuert das Laden, wenn sich das Fahrzeug am in der Tesla-Navigation hinterlegten Zuhause befindet. Hinweis: Unterschiedliche Fahrerprofile mit unterschiedlichen Zuhause-Adressen können dies beeinträchtigen. Stelle sicher, dass der letzte Fahrer in der Tesla-Navigation die korrekte Zuhause-Adresse eingestellt hat."));

        Register(TranslationKeys.CarSettingsNavigationWorkDetection,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla navigation Work used for home detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Navigation Arbeit wird zur Zuhause-Erkennung verwendet"));

        Register(TranslationKeys.CarSettingsNavigationWorkDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "TSC will manage charging if the car is at the work location set in the Tesla navigation system. Note: Different driver profiles with different work addresses might mess this up. Make sure that the last driver has the correct work address set in the Tesla navigation system."),
            new TextLocalizationTranslation(LanguageCodes.German, "TSC steuert das Laden, wenn sich das Fahrzeug am in der Tesla-Navigation hinterlegten Arbeitsort befindet. Hinweis: Unterschiedliche Fahrerprofile mit unterschiedlichen Arbeitsadressen können dies beeinträchtigen. Stelle sicher, dass der letzte Fahrer die korrekte Arbeitsadresse in der Tesla-Navigation gesetzt hat."));

        Register(TranslationKeys.CarSettingsNavigationFavoriteDetection,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla navigation Favorite used for home detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Navigation Favorit wird zur Zuhause-Erkennung verwendet"));

        Register(TranslationKeys.CarSettingsNavigationFavoriteDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "TSC will manage charging if the car is at any favorite location set in the Tesla navigation system. Note: TSC is unable to detect at which favorite location the car is, so if you charge at a public charger that is configured as favorite in the Tesla navigation system, TSC tries to manage charging based on your solar production at home and might stop charging if you don't have any solar production at home. Moreover, different driver profiles with different favorite addresses might mess this up. It is HIGHLY recommended to only have one favorite location if this setting is enabled."),
            new TextLocalizationTranslation(LanguageCodes.German, "TSC steuert das Laden, wenn sich das Fahrzeug an einem in der Tesla-Navigation hinterlegten Favoriten befindet. Hinweis: TSC kann nicht erkennen, an welchem Favoriten sich das Fahrzeug befindet. Wenn du an einer öffentlichen Ladestation lädst, die als Favorit hinterlegt ist, versucht TSC das Laden anhand deiner Solarproduktion zu Hause zu steuern und könnte das Laden stoppen, wenn du keine Solarproduktion hast. Unterschiedliche Fahrerprofile mit unterschiedlichen Favoritenadressen können dies ebenfalls beeinträchtigen. Es wird DRINGEND empfohlen, bei aktivierter Einstellung nur einen Favoriten zu hinterlegen."));

        Register(TranslationKeys.CarSettingsHomeDetectionVia,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Detection via"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause-Erkennung über"));

        Register(TranslationKeys.CarSettingsBlePairingSection,
            new TextLocalizationTranslation(LanguageCodes.English, "BLE Pairing and test"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Kopplung und Test"));

        Register(TranslationKeys.CarSettingsBleRateLimitDescriptionPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "To come around rate limits TSC can use BLE instead of the Fleet API. This requires "),
            new TextLocalizationTranslation(LanguageCodes.German, "Um Rate Limits zu umgehen, kann TSC BLE statt der Fleet-API verwenden. Dafür ist "));

        Register(TranslationKeys.CarSettingsSettingUpBleApi,
            new TextLocalizationTranslation(LanguageCodes.English, "setting up a BLE API"),
            new TextLocalizationTranslation(LanguageCodes.German, "die Einrichtung einer BLE-API"));

        Register(TranslationKeys.CarSettingsBlePairingNote,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: When clicking the pair button the car won't display any feedback. You have to place the card on the center console. Only after doing so, a message will pop up. If you don't see a message, the pairing failed. As the car does not send any feedback, just try a few times, if it still does not work reboot your BLE device."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Wenn du auf die Kopplungsschaltfläche klickst, zeigt das Fahrzeug keine Rückmeldung an. Lege die Karte auf die Mittelkonsole. Erst danach erscheint eine Meldung. Wenn du keine Meldung siehst, war die Kopplung erfolglos. Da das Fahrzeug keine Rückmeldung sendet, probiere es einige Male. Wenn es immer noch nicht klappt, starte dein BLE-Gerät neu."));

        Register(TranslationKeys.CarSettingsBlePairButton,
            new TextLocalizationTranslation(LanguageCodes.English, "BLE Pair"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE koppeln"));

        Register(TranslationKeys.CarSettingsTestBleAccessButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Test BLE access"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Zugriff testen"));

        Register(TranslationKeys.CarSettingsTestBleAccessPrerequisite,
            new TextLocalizationTranslation(LanguageCodes.English, "Before you can test BLE access you must pair the car with TSC. This includes placing the card on your center console and confirming the new \"phone key\" on the car's screen."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bevor du den BLE-Zugriff testen kannst, musst du das Fahrzeug mit TSC koppeln. Lege dazu die Karte auf die Mittelkonsole und bestätige den neuen \"Phone Key\" auf dem Fahrzeugbildschirm."));

        Register(TranslationKeys.CarSettingsTestBleAccessResult,
            new TextLocalizationTranslation(LanguageCodes.English, "After clicking the test button the car 's current should be set to 7A. Note: The car needs to be awake for this test."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nachdem du die Testschaltfläche geklickt hast, sollte der Ladestrom des Fahrzeugs auf 7A gesetzt werden. Hinweis: Für diesen Test muss das Fahrzeug wach sein."));

        Register(TranslationKeys.CarSettingsSetTo7AButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set to 7A"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auf 7A setzen"));

        Register(TranslationKeys.CarSettingsTestWakeupButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Test Wakeup via BLE"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aufwecken über BLE testen"));

        Register(TranslationKeys.CarSettingsTestWakeupResult,
            new TextLocalizationTranslation(LanguageCodes.English, "After this test the car should wake up."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nach diesem Test sollte das Fahrzeug aufwachen."));

        Register(TranslationKeys.CarSettingsWakeUpButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Wake up"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aufwecken"));

        Register(TranslationKeys.CarSettingsDeserializeError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not deserialize message from TSC."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nachricht von TSC konnte nicht deserialisiert werden."));

        Register(TranslationKeys.CarSettingsBleAccessSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "Ble access seems to work. Please double check if the charge current was set to 7A. Note: As TSC starts using BLE as soon as it is working you might see the 7A only for a short time as TSC changes it every 30 seconds by default."),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Zugriff scheint zu funktionieren. Bitte prüfe, ob der Ladestrom auf 7A gesetzt wurde. Hinweis: Sobald BLE funktioniert, verwendet TSC es automatisch. Daher siehst du die 7A möglicherweise nur kurz, da TSC standardmäßig alle 30 Sekunden den Wert ändert."));

        Register(TranslationKeys.CarSettingsWakeCallAccepted,
            new TextLocalizationTranslation(LanguageCodes.English, "The car accepted the wake call."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Fahrzeug hat den Aufweckruf akzeptiert."));
    }
}
