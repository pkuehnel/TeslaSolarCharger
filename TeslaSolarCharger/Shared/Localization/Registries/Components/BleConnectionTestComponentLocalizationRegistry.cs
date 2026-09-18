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

        //Deliberately says nothing about the key: the request that a sleeping car still answers needs no key at all,
        //so there is no evidence either way while the car sleeps.
        Register(TranslationKeys.BleTestCarAsleep,
            new TextLocalizationTranslation(LanguageCodes.English,
                "The car is in range but asleep. Open a door to wake it up and test again."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Das Fahrzeug ist in Reichweite, schläft aber. Öffnen Sie eine Tür, um es aufzuwecken, und testen Sie erneut."));

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
                "The car is in range but the request failed. Please test again."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Das Fahrzeug ist in Reichweite, die Anfrage ist aber fehlgeschlagen. Bitte testen Sie erneut."));

        Register(TranslationKeys.BleTestAgainButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Test again"),
            new TextLocalizationTranslation(LanguageCodes.German, "Erneut testen"));

        Register(TranslationKeys.BleTestDetails,
            new TextLocalizationTranslation(LanguageCodes.English, "Details: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Details: {0}"));

        Register(TranslationKeys.BleTestPairKeyTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Add TSC's key to the car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schlüssel von TSC im Fahrzeug hinterlegen"));

        //Two steps, and both are easy to miss: the card tap alone does not add the key, the message on the car's
        //screen has to be confirmed as well.
        Register(TranslationKeys.BleTestPairKeyHint,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Go to the car and wake it up, e.g. by opening a door. After clicking the button you have 30 seconds to hold one of your key cards against the center console. The car then shows a request on its touchscreen that you have to confirm."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Gehen Sie zum Fahrzeug und wecken Sie es auf, zum Beispiel durch das Öffnen einer Tür. Nach dem Klick auf die Schaltfläche haben Sie 30 Sekunden Zeit, eine Ihrer Schlüsselkarten an die Mittelkonsole zu halten. Danach zeigt das Fahrzeug auf dem Touchscreen eine Anfrage an, die Sie bestätigen müssen."));

        Register(TranslationKeys.BleTestPairKeyButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add key"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schlüssel hinzufügen"));

        //Only the request is being sent here; the car waits for the key card on its own time, so this may not
        //pretend to be waiting for the user's tap.
        Register(TranslationKeys.BleTestPairing,
            new TextLocalizationTranslation(LanguageCodes.English, "Sending the request to the car..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Anfrage wird an das Fahrzeug gesendet..."));

        //The car only gets the key once the user tapped a key card AND confirmed the request on the car's screen, so
        //this may neither claim the key was added nor stop after the tap.
        Register(TranslationKeys.BleTestPairKeySuccess,
            new TextLocalizationTranslation(LanguageCodes.English,
                "The request was sent to the car. Now do this in the car: 1. Hold one of your key cards against the center console. 2. Confirm the request the car then shows on its touchscreen. After that, test the connection again."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Die Anfrage wurde an das Fahrzeug gesendet. Gehen Sie jetzt im Fahrzeug so vor: 1. Halten Sie eine Ihrer Schlüsselkarten an die Mittelkonsole. 2. Bestätigen Sie die Anfrage, die das Fahrzeug daraufhin auf dem Touchscreen anzeigt. Testen Sie anschließend die Verbindung erneut."));

        Register(TranslationKeys.BleTestPairKeyError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not add the key: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Schlüssel konnte nicht hinzugefügt werden: {0}"));
    }
}
