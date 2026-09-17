namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class BleConnectionTestComponentLocalizationRegistry : TextLocalizationRegistry<BleConnectionTestComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.BleTestTesting,
            new TextLocalizationTranslation(LanguageCodes.English, "Testing the BLE connection can take up to 30 seconds..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Testen der BLE-Verbindung kann bis zu 30 Sekunden dauern..."));

        Register(TranslationKeys.BleTestSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "BLE connection is working."),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Verbindung funktioniert."));

        Register(TranslationKeys.BleTestCarNotFound,
            new TextLocalizationTranslation(LanguageCodes.English,
                "The BLE container did not hear the car. Make sure the car is parked within range of the container's antenna and test again."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Der BLE-Container hat das Fahrzeug nicht gehört. Stellen Sie sicher, dass das Fahrzeug in Reichweite der Antenne des Containers steht, und testen Sie erneut."));

        Register(TranslationKeys.BleTestCarAsleep,
            new TextLocalizationTranslation(LanguageCodes.English,
                "The car is in range and TSC's key works, but the car is asleep. Open a door to wake it up and test again."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Das Fahrzeug ist in Reichweite und der Schlüssel von TSC funktioniert, aber das Fahrzeug schläft. Öffnen Sie eine Tür, um es aufzuwecken, und testen Sie erneut."));

        Register(TranslationKeys.BleTestKeyNotPaired,
            new TextLocalizationTranslation(LanguageCodes.English,
                "The car is in range but TSC can not establish a secure connection to it. In almost all cases this means TSC's key is not added to the car yet."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Das Fahrzeug ist in Reichweite, aber TSC kann keine sichere Verbindung aufbauen. Fast immer bedeutet das, dass der Schlüssel von TSC noch nicht im Fahrzeug hinterlegt ist."));

        Register(TranslationKeys.BleTestContainerProblem,
            new TextLocalizationTranslation(LanguageCodes.English,
                "The BLE container could not be reached or its Bluetooth adapter could not be used, so the car was never asked. Check the BLE URL, the selected adapter and the container's logs."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Der BLE-Container war nicht erreichbar oder sein Bluetooth-Adapter konnte nicht verwendet werden, das Fahrzeug wurde daher gar nicht angefragt. Prüfen Sie die BLE-URL, den ausgewählten Adapter und die Logs des Containers."));

        Register(TranslationKeys.BleTestUnknown,
            new TextLocalizationTranslation(LanguageCodes.English,
                "The car is in range and TSC's key works, but the request failed. Please test again."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Das Fahrzeug ist in Reichweite und der Schlüssel von TSC funktioniert, die Anfrage ist aber fehlgeschlagen. Bitte testen Sie erneut."));

        Register(TranslationKeys.BleTestAgainButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Test again"),
            new TextLocalizationTranslation(LanguageCodes.German, "Erneut testen"));

        Register(TranslationKeys.BleTestDetails,
            new TextLocalizationTranslation(LanguageCodes.English, "Details: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Details: {0}"));

        Register(TranslationKeys.BleTestPairKeyTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Add TSC's key to the car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schlüssel von TSC im Fahrzeug hinterlegen"));

        Register(TranslationKeys.BleTestPairKeyHint,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Go to the car and wake it up, e.g. by opening a door. After clicking the button, confirm the new key within 30 seconds by holding one of your key cards against the center console."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Gehen Sie zum Fahrzeug und wecken Sie es auf, zum Beispiel durch das Öffnen einer Tür. Bestätigen Sie den neuen Schlüssel nach dem Klick auf die Schaltfläche innerhalb von 30 Sekunden, indem Sie eine Ihrer Schlüsselkarten an die Mittelkonsole halten."));

        Register(TranslationKeys.BleTestPairKeyButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add key"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schlüssel hinzufügen"));

        Register(TranslationKeys.BleTestPairing,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for the confirmation in the car..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Warten auf die Bestätigung im Fahrzeug..."));

        Register(TranslationKeys.BleTestPairKeySuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "The key was added. Test the connection again."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Schlüssel wurde hinzugefügt. Testen Sie die Verbindung erneut."));

        Register(TranslationKeys.BleTestPairKeyError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not add the key: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Schlüssel konnte nicht hinzugefügt werden: {0}"));
    }
}
