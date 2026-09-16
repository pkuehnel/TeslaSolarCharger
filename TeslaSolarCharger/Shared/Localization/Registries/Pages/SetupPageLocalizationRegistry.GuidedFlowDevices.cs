namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

/// <summary>The words of the per car and per charger stages, and of the closing behaviour summary.</summary>
public partial class SetupPageLocalizationRegistry
{
    private void RegisterCarTexts()
    {
        Register(TranslationKeys.SetupCarNotFound,
            new TextLocalizationTranslation(LanguageCodes.English, "This car is not part of your setup any more."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Auto gehört nicht mehr zu Ihrer Einrichtung."));

        Register(TranslationKeys.SetupCarStageProgressFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Step {0} of {1}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schritt {0} von {1}"));

        Register(TranslationKeys.SetupCarStageIdentifyTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Which car is this?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Welches Auto ist das?"));

        Register(TranslationKeys.SetupCarStageConnectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "How should we reach this car?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie sollen wir dieses Auto erreichen?"));

        Register(TranslationKeys.SetupCarStageConnectTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Let us connect"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindung herstellen"));

        Register(TranslationKeys.SetupCarStageChargingTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Technical details of the car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Technische Daten des Autos"));

        Register(TranslationKeys.SetupCarStageReviewTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What we have set up"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das haben wir eingerichtet"));

        Register(TranslationKeys.SetupCarDoneButton,
            new TextLocalizationTranslation(LanguageCodes.English, "This car is done"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Auto ist fertig"));

        Register(TranslationKeys.SetupCarIdentifyIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Tell us what the car is. We work out from that how we can reach it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sagen Sie uns, was für ein Auto es ist. Daraus leiten wir ab, wie wir es erreichen können."));

        Register(TranslationKeys.SetupCarMakeLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Make"),
            new TextLocalizationTranslation(LanguageCodes.German, "Marke"));

        Register(TranslationKeys.SetupCarMakeHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "For example Tesla, Hyundai, Volkswagen."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zum Beispiel Tesla, Hyundai, Volkswagen."));

        Register(TranslationKeys.SetupCarNameLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "What do you call this car?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie nennen Sie dieses Auto?"));

        Register(TranslationKeys.SetupCarNameHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "Only so you can tell it apart from your other cars."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nur damit Sie es von Ihren anderen Autos unterscheiden können."));

        Register(TranslationKeys.SetupCarVinLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Vehicle identification number"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrgestellnummer"));

        Register(TranslationKeys.SetupCarVinHelpTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Where do I find this number?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wo finde ich diese Nummer?"));

        Register(TranslationKeys.SetupCarVinHelpBody,
            new TextLocalizationTranslation(LanguageCodes.English, "It is 17 letters and digits. You will find it in your car's app, in the vehicle registration document, and on a small plate at the bottom of the windscreen on the driver's side."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie besteht aus 17 Buchstaben und Ziffern. Sie finden sie in der App Ihres Autos, im Fahrzeugschein und auf einem kleinen Schild unten an der Windschutzscheibe auf der Fahrerseite."));

        Register(TranslationKeys.SetupCarVinFromTeslaAccount,
            new TextLocalizationTranslation(LanguageCodes.English, "This came from your Tesla account, so it cannot be changed here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das stammt aus Ihrem Tesla-Konto und kann hier nicht geändert werden."));

        Register(TranslationKeys.SetupCarConnectionIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Pick how this car should be reached. Each option says what it needs and what it costs."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wählen Sie, wie dieses Auto erreicht werden soll. Bei jeder Option steht, was sie braucht und was sie kostet."));

        Register(TranslationKeys.SetupCarBluetoothReadinessQuestion,
            new TextLocalizationTranslation(LanguageCodes.English, "Do you already have a device near the parking space?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Haben Sie bereits ein Gerät am Stellplatz?"));

        Register(TranslationKeys.SetupCarBluetoothReadinessExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Bluetooth only reaches a few metres, so something has to sit close to where the car parks and stay switched on."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bluetooth reicht nur wenige Meter, daher muss etwas nah am Stellplatz stehen und eingeschaltet bleiben."));

        Register(TranslationKeys.SetupCarBluetoothReadinessHaveOne,
            new TextLocalizationTranslation(LanguageCodes.English, "Yes, I have one"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ja, ich habe eines"));

        Register(TranslationKeys.SetupCarBluetoothReadinessNeedOne,
            new TextLocalizationTranslation(LanguageCodes.English, "Not yet"),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch nicht"));

        Register(TranslationKeys.SetupCarBluetoothNeedsDeviceHint,
            new TextLocalizationTranslation(LanguageCodes.English, "You can carry on and come back to this car once the device is in place. Nothing else you set up is affected."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie können fortfahren und zu diesem Auto zurückkehren, sobald das Gerät steht. Alles andere bleibt davon unberührt."));

        Register(TranslationKeys.SetupCarBatteryLevelQuestion,
            new TextLocalizationTranslation(LanguageCodes.English, "How should we know how full the battery is?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Woher sollen wir wissen, wie voll der Akku ist?"));

        Register(TranslationKeys.SetupCarBatteryLevelExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Without a connection to the car we cannot read the battery level."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ohne Verbindung zum Auto können wir den Ladestand nicht auslesen."));

        Register(TranslationKeys.SetupCarBatteryLevelManual,
            new TextLocalizationTranslation(LanguageCodes.English, "I will enter it myself"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ich gebe ihn selbst ein"));

        Register(TranslationKeys.SetupCarBatteryLevelEstimated,
            new TextLocalizationTranslation(LanguageCodes.English, "Work it out from what was charged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus der geladenen Energie ermitteln"));

        Register(TranslationKeys.SetupCarBatteryLevelConsequence,
            new TextLocalizationTranslation(LanguageCodes.English, "Either way the figure can drift, so targets given as a percentage are approximate for this car."),
            new TextLocalizationTranslation(LanguageCodes.German, "In beiden Fällen kann der Wert abweichen, daher sind Prozentziele bei diesem Auto nur ungefähr."));

        RegisterCarConnectTexts();
        RegisterCarChargingTexts();
        RegisterCarReviewTexts();
    }

    private void RegisterCarConnectTexts()
    {
        Register(TranslationKeys.SetupCarBluetoothIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Three things have to line up: we have to reach the device, the device has to use the right radio, and your car has to accept our key."),
            new TextLocalizationTranslation(LanguageCodes.German, "Drei Dinge müssen zusammenpassen: Wir müssen das Gerät erreichen, das Gerät muss das richtige Funkmodul verwenden, und Ihr Auto muss unseren Schlüssel akzeptieren."));

        Register(TranslationKeys.SetupCarBluetoothStepReachTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "1. Where is the device?"),
            new TextLocalizationTranslation(LanguageCodes.German, "1. Wo steht das Gerät?"));

        Register(TranslationKeys.SetupCarBluetoothUrlLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Address of the Bluetooth device"),
            new TextLocalizationTranslation(LanguageCodes.German, "Adresse des Bluetooth-Geräts"));

        Register(TranslationKeys.SetupCarBluetoothUrlHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "The address the Bluetooth add-on runs on in your home network, including the port."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Adresse, unter der die Bluetooth-Erweiterung in Ihrem Heimnetz läuft, samt Port."));

        Register(TranslationKeys.SetupCarBluetoothStepAdapterTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "2. Which radio should it use?"),
            new TextLocalizationTranslation(LanguageCodes.German, "2. Welches Funkmodul soll es verwenden?"));

        Register(TranslationKeys.SetupCarBluetoothNoAdaptersHint,
            new TextLocalizationTranslation(LanguageCodes.English, "We could not reach the device yet, so there is nothing to choose from. Check the address above."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Gerät ist noch nicht erreichbar, daher gibt es nichts zur Auswahl. Prüfen Sie die Adresse oben."));

        Register(TranslationKeys.SetupCarBluetoothSingleAdapterHint,
            new TextLocalizationTranslation(LanguageCodes.English, "The device has one radio, so we use it. Nothing to choose here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Gerät hat ein Funkmodul, das wir verwenden. Hier gibt es nichts auszuwählen."));

        Register(TranslationKeys.SetupCarBluetoothAdapterLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Radio to use for this car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Funkmodul für dieses Auto"));

        Register(TranslationKeys.SetupCarBluetoothAdapterHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "Pick the one closest to where this car parks."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wählen Sie das Modul, das dem Stellplatz dieses Autos am nächsten ist."));

        Register(TranslationKeys.SetupCarBluetoothAdapterDefault,
            new TextLocalizationTranslation(LanguageCodes.English, "Let the device decide"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Gerät entscheiden lassen"));

        Register(TranslationKeys.SetupCarBluetoothAdapterUsb,
            new TextLocalizationTranslation(LanguageCodes.English, "Plugged-in adapter"),
            new TextLocalizationTranslation(LanguageCodes.German, "Eingesteckter Adapter"));

        Register(TranslationKeys.SetupCarBluetoothAdapterOnboard,
            new TextLocalizationTranslation(LanguageCodes.English, "Built-in radio"),
            new TextLocalizationTranslation(LanguageCodes.German, "Eingebautes Funkmodul"));

        Register(TranslationKeys.SetupCarBluetoothStepPairTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "3. Let the car accept our key"),
            new TextLocalizationTranslation(LanguageCodes.German, "3. Das Auto unseren Schlüssel akzeptieren lassen"));

        Register(TranslationKeys.SetupCarBluetoothPairInstructions,
            new TextLocalizationTranslation(LanguageCodes.English, "Have your key card ready and be at the car. When we ask to pair, tap the key card on the centre console and confirm on the car's screen."),
            new TextLocalizationTranslation(LanguageCodes.German, "Halten Sie Ihre Keycard bereit und stehen Sie am Auto. Wenn wir das Koppeln anfragen, legen Sie die Keycard auf die Mittelkonsole und bestätigen Sie auf dem Bildschirm des Autos."));

        Register(TranslationKeys.SetupCarBluetoothTestButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Save and try to reach the car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Speichern und das Auto zu erreichen versuchen"));

        Register(TranslationKeys.SetupCarNeedsIdentityBeforeTest,
            new TextLocalizationTranslation(LanguageCodes.English, "Go back and give this car a name and its identification number first; we need those before we can try."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gehen Sie zurück und geben Sie dem Auto zuerst einen Namen und die Fahrgestellnummer; ohne diese können wir es nicht versuchen."));

        Register(TranslationKeys.SetupCarTeslaCloudIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla has to know that this app may control your car. That is a one-off approval you give at Tesla."),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla muss wissen, dass diese App Ihr Auto steuern darf. Das ist eine einmalige Freigabe, die Sie bei Tesla erteilen."));

        Register(TranslationKeys.SetupCarTeslaCloudRequirements,
            new TextLocalizationTranslation(LanguageCodes.English, "This car needs internet access and its own subscription. The car also has to be awake for the first check."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Auto benötigt Internetzugang und ein eigenes Abonnement. Für die erste Prüfung muss das Auto außerdem wach sein."));

        Register(TranslationKeys.SetupCarTeslaCloudLicenceMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "This car has no subscription yet, so we cannot control it over the internet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Für dieses Auto besteht noch kein Abonnement, daher können wir es nicht über das Internet steuern."));

        Register(TranslationKeys.SetupCarTelemetryIncompatibleFallback,
            new TextLocalizationTranslation(LanguageCodes.English, "This car's hardware cannot send us live data, so we ask it for its state instead. Everything still works, we just ask a little less often."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Hardware dieses Autos kann uns keine Live-Daten senden, daher fragen wir seinen Zustand ab. Es funktioniert weiterhin alles, wir fragen nur etwas seltener."));

        Register(TranslationKeys.SetupCarTeslaCloudAsleepNote,
            new TextLocalizationTranslation(LanguageCodes.English, "If the car is asleep or away, your settings are still saved. Come back and try the check when it is back."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wenn das Auto schläft oder unterwegs ist, bleiben Ihre Einstellungen trotzdem gespeichert. Prüfen Sie erneut, sobald es zurück ist."));

        Register(TranslationKeys.SetupCarSmartCarIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Connecting your car account lets us read how full the battery is."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wenn Sie Ihr Fahrzeugkonto verbinden, können wir den Ladestand auslesen."));

        Register(TranslationKeys.SetupCarSmartCarStillNeedsCharger,
            new TextLocalizationTranslation(LanguageCodes.English, "This only supplies data. The charging itself is still done by your charging station, so that has to be set up too."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das liefert nur Daten. Geladen wird weiterhin über Ihre Ladestation, die deshalb ebenfalls eingerichtet werden muss."));

        Register(TranslationKeys.SetupCarSmartCarConnected,
            new TextLocalizationTranslation(LanguageCodes.English, "Connected. We can read this car's battery level."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbunden. Wir können den Ladestand dieses Autos auslesen."));

        Register(TranslationKeys.SetupCarSmartCarConnectButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect my car account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugkonto verbinden"));

        Register(TranslationKeys.SetupCarSmartCarBillingConfirmTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "This can cost extra"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das kann zusätzlich kosten"));

        Register(TranslationKeys.SetupCarSmartCarBillingConfirmText,
            new TextLocalizationTranslation(LanguageCodes.English, "On the next page you pick which cars to share. Every car you pick beyond the subscriptions you already have is booked and charged automatically. Pick only the cars you want to use here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Auf der nächsten Seite wählen Sie, welche Autos freigegeben werden. Jedes Auto über Ihre vorhandenen Abonnements hinaus wird automatisch gebucht und berechnet. Wählen Sie nur die Autos, die Sie hier nutzen möchten."));

        Register(TranslationKeys.SetupCarSmartCarBillingConfirmButton,
            new TextLocalizationTranslation(LanguageCodes.English, "I understand, continue"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verstanden, weiter"));

        Register(TranslationKeys.SetupCarSmartCarUrlMissingError,
            new TextLocalizationTranslation(LanguageCodes.English, "We could not open the connection page. Please try again in a moment."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Verbindungsseite konnte nicht geöffnet werden. Bitte versuchen Sie es gleich noch einmal."));

        Register(TranslationKeys.SetupCarChargingStationOnlyIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "This car is controlled entirely by the charging station it is plugged into."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Auto wird vollständig über die Ladestation gesteuert, an der es hängt."));

        Register(TranslationKeys.SetupCarChargingStationOnlyNothingToConnect,
            new TextLocalizationTranslation(LanguageCodes.English, "There is nothing to connect to the car itself. Set up the charging station and tell it which car may charge there."),
            new TextLocalizationTranslation(LanguageCodes.German, "Mit dem Auto selbst ist nichts zu verbinden. Richten Sie die Ladestation ein und legen Sie fest, welches Auto dort laden darf."));

        Register(TranslationKeys.SetupCarGoToChargersButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Go to my charging stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu meinen Ladestationen"));
    }

    private void RegisterCarChargingTexts()
    {
        Register(TranslationKeys.SetupCarElectricalExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "These come from the car and your wiring. If you are unsure, the car's manual lists them."),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Werte ergeben sich aus dem Auto und Ihrer Installation. Im Zweifel stehen sie im Handbuch des Autos."));

        Register(TranslationKeys.SetupCarMaximumCurrentLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Highest charging current"),
            new TextLocalizationTranslation(LanguageCodes.German, "Höchster Ladestrom"));

        Register(TranslationKeys.SetupCarMaximumCurrentHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "The most this car may draw. 16 A is typical for a home installation."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Maximum, das dieses Auto ziehen darf. 16 A sind bei einer Hausinstallation üblich."));

        Register(TranslationKeys.SetupCarPhasesLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "How the car charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie das Auto lädt"));

        Register(TranslationKeys.SetupCarPhasesHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "Three-phase charging is faster. Most European cars can do it; many smaller ones cannot."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dreiphasiges Laden ist schneller. Die meisten europäischen Autos können das, viele kleinere nicht."));

        Register(TranslationKeys.SetupCarPhasesSingle,
            new TextLocalizationTranslation(LanguageCodes.English, "On one phase"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einphasig"));

        Register(TranslationKeys.SetupCarPhasesThree,
            new TextLocalizationTranslation(LanguageCodes.English, "On three phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dreiphasig"));

        Register(TranslationKeys.SetupCarBatterySizeLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Usable battery size"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nutzbare Akkukapazität"));

        Register(TranslationKeys.SetupCarBatterySizeHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "We use this to work out how long charging will take."),
            new TextLocalizationTranslation(LanguageCodes.German, "Damit berechnen wir, wie lange das Laden dauert."));

        Register(TranslationKeys.SetupCarBatterySizeUnknownHint,
            new TextLocalizationTranslation(LanguageCodes.English, "You will find this in the car's manual or data sheet, usually as \"usable capacity\" in kWh."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie finden das im Handbuch oder Datenblatt des Autos, meist als „nutzbare Kapazität“ in kWh."));
    }

    private void RegisterCarReviewTexts()
    {
        Register(TranslationKeys.SetupCarReviewIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Here is what this car will do. Nothing starts until you finish the assistant."),
            new TextLocalizationTranslation(LanguageCodes.German, "So verhält sich dieses Auto. Es startet nichts, bevor Sie den Assistenten abschließen."));

        Register(TranslationKeys.SetupCarAssignmentTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Where this car charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wo dieses Auto lädt"));

        Register(TranslationKeys.SetupCarAssignmentAutomaticFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "We assigned it to {0}, because that is your only charging point."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir haben es {0} zugeordnet, da dies Ihr einziger Ladepunkt ist."));

        Register(TranslationKeys.SetupCarAssignmentChoose,
            new TextLocalizationTranslation(LanguageCodes.English, "Tick the charging points this car may use."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wählen Sie die Ladepunkte aus, die dieses Auto nutzen darf."));

        Register(TranslationKeys.SetupCarReviewStatusTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Where this car stands"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stand bei diesem Auto"));

        Register(TranslationKeys.SetupCarReviewConfiguration,
            new TextLocalizationTranslation(LanguageCodes.English, "Settings:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einstellungen:"));

        Register(TranslationKeys.SetupCarReviewConfigurationComplete,
            new TextLocalizationTranslation(LanguageCodes.English, "complete"),
            new TextLocalizationTranslation(LanguageCodes.German, "vollständig"));

        Register(TranslationKeys.SetupCarReviewConfigurationIncomplete,
            new TextLocalizationTranslation(LanguageCodes.English, "something is still missing"),
            new TextLocalizationTranslation(LanguageCodes.German, "es fehlt noch etwas"));

        Register(TranslationKeys.SetupCarReviewActivation,
            new TextLocalizationTranslation(LanguageCodes.English, "Automatic charging:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Automatisches Laden:"));

        Register(TranslationKeys.SetupCarReviewActivationActive,
            new TextLocalizationTranslation(LanguageCodes.English, "already running"),
            new TextLocalizationTranslation(LanguageCodes.German, "läuft bereits"));

        Register(TranslationKeys.SetupCarReviewActivationReady,
            new TextLocalizationTranslation(LanguageCodes.English, "will start when you finish"),
            new TextLocalizationTranslation(LanguageCodes.German, "startet, wenn Sie abschließen"));

        Register(TranslationKeys.SetupCarReviewActivationBlocked,
            new TextLocalizationTranslation(LanguageCodes.English, "cannot start yet, see below"),
            new TextLocalizationTranslation(LanguageCodes.German, "kann noch nicht starten, siehe unten"));

        Register(TranslationKeys.SetupCarReviewConnectionCheck,
            new TextLocalizationTranslation(LanguageCodes.English, "Connection check:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindungsprüfung:"));

        Register(TranslationKeys.SetupCarReviewCheckSucceeded,
            new TextLocalizationTranslation(LanguageCodes.English, "we reached the car"),
            new TextLocalizationTranslation(LanguageCodes.German, "wir haben das Auto erreicht"));

        Register(TranslationKeys.SetupCarReviewCheckFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "we could not reach the car"),
            new TextLocalizationTranslation(LanguageCodes.German, "wir konnten das Auto nicht erreichen"));

        Register(TranslationKeys.SetupCarReviewCheckPending,
            new TextLocalizationTranslation(LanguageCodes.English, "still trying"),
            new TextLocalizationTranslation(LanguageCodes.German, "läuft noch"));

        Register(TranslationKeys.SetupCarReviewCheckNotRun,
            new TextLocalizationTranslation(LanguageCodes.English, "not tried yet"),
            new TextLocalizationTranslation(LanguageCodes.German, "noch nicht versucht"));
    }

    private void RegisterChargerTexts()
    {
        Register(TranslationKeys.SetupChargerNotFound,
            new TextLocalizationTranslation(LanguageCodes.English, "This charging station is not part of your setup any more."),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Ladestation gehört nicht mehr zu Ihrer Einrichtung."));

        Register(TranslationKeys.SetupChargerStageConnectTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Let the charging station find us"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Ladestation soll uns finden"));

        Register(TranslationKeys.SetupChargerStageSettingsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What this charging station can do"),
            new TextLocalizationTranslation(LanguageCodes.German, "Was diese Ladestation kann"));

        Register(TranslationKeys.SetupChargerStageReviewTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What we have set up"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das haben wir eingerichtet"));

        Register(TranslationKeys.SetupChargerDoneButton,
            new TextLocalizationTranslation(LanguageCodes.English, "This charging station is done"),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Ladestation ist fertig"));

        Register(TranslationKeys.SetupChargerWaitingBeforeNext,
            new TextLocalizationTranslation(LanguageCodes.English, "There is nothing to set up until the charging station has reported in."),
            new TextLocalizationTranslation(LanguageCodes.German, "Vor der ersten Meldung der Ladestation gibt es nichts einzurichten."));

        Register(TranslationKeys.SetupChargerConnectIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging stations announce themselves. You give yours an address to call, and it appears here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen melden sich selbst. Sie geben Ihrer Station eine Adresse, die sie anruft – dann erscheint sie hier."));

        Register(TranslationKeys.SetupChargerWhereToLookTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Where to look in your charger's settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wo Sie in den Einstellungen Ihrer Ladestation suchen"));

        Register(TranslationKeys.SetupChargerWhereToLookBody,
            new TextLocalizationTranslation(LanguageCodes.English, "In your charger's app or web page, look for a section called OCPP, back end, central system or load management. There will be a field for an address and often one for an identifier. If you cannot find it, the charger's manual will say whether it supports OCPP at all."),
            new TextLocalizationTranslation(LanguageCodes.German, "Suchen Sie in der App oder Weboberfläche Ihrer Ladestation nach einem Bereich namens OCPP, Backend, Zentralsystem oder Lastmanagement. Dort gibt es ein Feld für eine Adresse und oft eines für eine Kennung. Falls Sie nichts finden, steht im Handbuch, ob die Station OCPP überhaupt unterstützt."));

        Register(TranslationKeys.SetupChargerNameLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "What do you call this charging station?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie nennen Sie diese Ladestation?"));

        Register(TranslationKeys.SetupChargerNameHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "Only so you can tell it apart from any others."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nur damit Sie sie von anderen unterscheiden können."));

        Register(TranslationKeys.SetupChargerChargepointIdLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Identifier"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kennung"));

        Register(TranslationKeys.SetupChargerChargepointIdHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "Anything short without spaces. If your charger already shows one, use that exactly."),
            new TextLocalizationTranslation(LanguageCodes.German, "Etwas Kurzes ohne Leerzeichen. Wenn Ihre Ladestation bereits eine anzeigt, verwenden Sie genau diese."));

        Register(TranslationKeys.SetupChargerAddressTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "The address to enter"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die einzutragende Adresse"));

        Register(TranslationKeys.SetupChargerAddressExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Copy this into the address field in your charger's settings, then save there."),
            new TextLocalizationTranslation(LanguageCodes.German, "Kopieren Sie das in das Adressfeld in den Einstellungen Ihrer Ladestation und speichern Sie dort."));

        Register(TranslationKeys.SetupChargerAddressIdPlaceholder,
            new TextLocalizationTranslation(LanguageCodes.English, "YOUR-IDENTIFIER"),
            new TextLocalizationTranslation(LanguageCodes.German, "IHRE-KENNUNG"));

        Register(TranslationKeys.SetupChargerCopyAddressButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Copy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kopieren"));

        Register(TranslationKeys.SetupChargerAddressProxyNote,
            new TextLocalizationTranslation(LanguageCodes.English, "Built from the address you are using right now. If you reach this app through a proxy or a different name, use the address your charger can reach instead."),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus der Adresse gebildet, die Sie gerade verwenden. Falls Sie diese App über einen Proxy oder einen anderen Namen erreichen, verwenden Sie die Adresse, die Ihre Ladestation erreichen kann."));

        Register(TranslationKeys.SetupChargerAddressCopied,
            new TextLocalizationTranslation(LanguageCodes.English, "Address copied."),
            new TextLocalizationTranslation(LanguageCodes.German, "Adresse kopiert."));

        Register(TranslationKeys.SetupChargerConnectedFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} has reported in. We can talk to it."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} hat sich gemeldet. Wir können mit der Station sprechen."));

        Register(TranslationKeys.SetupChargerEnterIdFirst,
            new TextLocalizationTranslation(LanguageCodes.English, "Enter an identifier above and we will show you the address to copy."),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie oben eine Kennung ein, dann zeigen wir Ihnen die Adresse zum Kopieren."));

        Register(TranslationKeys.SetupChargerWaitingForConnection,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for your charging station to call…"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir warten auf den Anruf Ihrer Ladestation …"));

        Register(TranslationKeys.SetupChargerWaitingHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Some chargers only call after you save the setting or restart them. This page keeps looking, so you can leave it open."),
            new TextLocalizationTranslation(LanguageCodes.German, "Manche Ladestationen melden sich erst nach dem Speichern oder einem Neustart. Diese Seite sucht weiter, Sie können sie offen lassen."));

        Register(TranslationKeys.SetupChargerSettingsIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "These limits come from your wiring and the charging station itself, not from the car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Grenzwerte ergeben sich aus Ihrer Installation und der Ladestation selbst, nicht aus dem Auto."));

        Register(TranslationKeys.SetupChargerConnectorChoiceTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Which charging point do you use?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Welchen Ladepunkt nutzen Sie?"));

        Register(TranslationKeys.SetupChargerConnectorChoiceExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "This charging station reported several charging points. Choose the one you plug your car into; we only take charge of that one."),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Ladestation hat mehrere Ladepunkte gemeldet. Wählen Sie den, an dem Sie Ihr Auto anschließen; nur um diesen kümmern wir uns."));

        Register(TranslationKeys.SetupChargerConnectorChoiceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging point"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepunkt"));

        Register(TranslationKeys.SetupChargerConnectorFallbackNameFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging point {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepunkt {0}"));

        Register(TranslationKeys.SetupChargerMaxCurrentLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Highest current this charging point may give"),
            new TextLocalizationTranslation(LanguageCodes.German, "Höchster Strom, den dieser Ladepunkt abgeben darf"));

        Register(TranslationKeys.SetupChargerMaxCurrentHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "What your installation allows. Your electrician will have told you, and it is usually on the charger's label."),
            new TextLocalizationTranslation(LanguageCodes.German, "Was Ihre Installation zulässt. Ihr Elektriker hat es Ihnen gesagt, meist steht es auch auf dem Typenschild."));

        Register(TranslationKeys.SetupChargerPhasesLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "How this charging point is wired"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie dieser Ladepunkt angeschlossen ist"));

        Register(TranslationKeys.SetupChargerPhasesHelper,
            new TextLocalizationTranslation(LanguageCodes.English, "If you are unsure, your electrician or the installation certificate will say."),
            new TextLocalizationTranslation(LanguageCodes.German, "Im Zweifel weiß es Ihr Elektriker oder es steht im Installationsprotokoll."));

        Register(TranslationKeys.SetupChargerPhasesSingle,
            new TextLocalizationTranslation(LanguageCodes.English, "One phase"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einphasig"));

        Register(TranslationKeys.SetupChargerPhasesThree,
            new TextLocalizationTranslation(LanguageCodes.English, "Three phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dreiphasig"));

        Register(TranslationKeys.SetupChargerAllowGuestsLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Other people's cars may charge here"),
            new TextLocalizationTranslation(LanguageCodes.German, "Autos anderer Personen dürfen hier laden"));

        Register(TranslationKeys.SetupChargerAllowGuestsHint,
            new TextLocalizationTranslation(LanguageCodes.English, "We cannot tell from the charging station alone which car is plugged in, so a guest's car is charged by the rules of this charging point rather than by any car's own settings."),
            new TextLocalizationTranslation(LanguageCodes.German, "Anhand der Ladestation allein können wir nicht erkennen, welches Auto angesteckt ist. Ein fremdes Auto lädt daher nach den Regeln dieses Ladepunkts und nicht nach den Einstellungen eines Autos."));

        Register(TranslationKeys.SetupChargerSaveError,
            new TextLocalizationTranslation(LanguageCodes.English, "This charging point could not be saved."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieser Ladepunkt konnte nicht gespeichert werden."));

        Register(TranslationKeys.SetupChargerReviewIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Here is what this charging station will do."),
            new TextLocalizationTranslation(LanguageCodes.German, "So verhält sich diese Ladestation."));

        Register(TranslationKeys.SetupChargerReviewCarsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars that may charge here"),
            new TextLocalizationTranslation(LanguageCodes.German, "Autos, die hier laden dürfen"));

        Register(TranslationKeys.SetupChargerReviewNoCars,
            new TextLocalizationTranslation(LanguageCodes.English, "None yet. You choose this on each car's own review screen."),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch keine. Das legen Sie in der Übersicht des jeweiligen Autos fest."));

        Register(TranslationKeys.SetupChargerReviewGuestsAllowed,
            new TextLocalizationTranslation(LanguageCodes.English, "Other people's cars may also charge here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Auch Autos anderer Personen dürfen hier laden."));

        Register(TranslationKeys.SetupChargerReviewGuestIdentificationNote,
            new TextLocalizationTranslation(LanguageCodes.English, "We cannot tell whose car it is, so such a charge follows this charging point's own limits."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir können nicht erkennen, wessen Auto es ist; ein solcher Ladevorgang folgt daher den Grenzwerten dieses Ladepunkts."));

        Register(TranslationKeys.SetupChargerReviewConnectorsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging points"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepunkte"));

        Register(TranslationKeys.SetupChargerReviewConnectorFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0}: up to {1} A on {2} phase(s)"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0}: bis zu {1} A auf {2} Phase(n)"));
    }

    private void RegisterFinishBehaviourTexts()
    {
        Register(TranslationKeys.SetupFinishBehaviourTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What will happen"),
            new TextLocalizationTranslation(LanguageCodes.German, "Was passieren wird"));

        Register(TranslationKeys.SetupFinishBehaviourCarBluetoothFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} is controlled over Bluetooth, from the device near where it parks."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} wird über Bluetooth gesteuert, vom Gerät in der Nähe des Stellplatzes."));

        Register(TranslationKeys.SetupFinishBehaviourCarTeslaAccountFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} is controlled over the internet, through your Tesla account."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} wird über das Internet gesteuert, mit Ihrem Tesla-Konto."));

        Register(TranslationKeys.SetupFinishBehaviourCarSmartCarFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} is charged by your charging station, and its battery level comes from your car account."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} wird von Ihrer Ladestation geladen, der Ladestand kommt aus Ihrem Fahrzeugkonto."));

        Register(TranslationKeys.SetupFinishBehaviourCarChargingStationFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} is charged by your charging station."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} wird von Ihrer Ladestation geladen."));

        Register(TranslationKeys.SetupFinishBehaviourCarUndecidedFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} has no way to be controlled yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} kann noch nicht gesteuert werden."));

        Register(TranslationKeys.SetupFinishBehaviourChargerAssignedFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} is assigned to your charging station."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} ist Ihrer Ladestation zugeordnet."));

        Register(TranslationKeys.SetupFinishBehaviourBatteryAutomatic,
            new TextLocalizationTranslation(LanguageCodes.English, "Your home battery keeps just enough charge for the evening, and your car gets the rest."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Hausspeicher behält genau so viel Ladung für den Abend, den Rest bekommt Ihr Auto."));

        Register(TranslationKeys.SetupFinishBehaviourBatteryManual,
            new TextLocalizationTranslation(LanguageCodes.English, "Your home battery keeps the reserve you set by hand."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Hausspeicher behält die von Ihnen selbst gesetzte Reserve."));

        Register(TranslationKeys.SetupFinishBehaviourFollowsSolarAndPrices,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging follows your solar production and the electricity price you entered."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Laden richtet sich nach Ihrer Solarproduktion und dem von Ihnen angegebenen Strompreis."));

        Register(TranslationKeys.SetupFinishBehaviourNoChargingTestYet,
            new TextLocalizationTranslation(LanguageCodes.English, "No real charging test has been done yet. You can try one from the start page whenever your car is plugged in."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ein echter Ladetest wurde noch nicht durchgeführt. Sie können ihn jederzeit von der Startseite aus starten, wenn Ihr Auto angesteckt ist."));
    }
}
