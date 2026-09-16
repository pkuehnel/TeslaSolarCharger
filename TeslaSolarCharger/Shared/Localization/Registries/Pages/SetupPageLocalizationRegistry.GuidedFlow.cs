namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

/// <summary>
/// The words of the guided journey: the equipment list, each car's stages and each charger's. Kept apart from the
/// rest of the setup texts only because there are a lot of them, not because they differ in kind.
/// </summary>
public partial class SetupPageLocalizationRegistry
{
    private void RegisterGuidedFlowTexts()
    {
        RegisterShellTexts();
        RegisterEquipmentTexts();
        RegisterRouteTexts();
        RegisterCarTexts();
        RegisterChargerTexts();
        RegisterFinishBehaviourTexts();
    }

    private void RegisterShellTexts()
    {
        Register(TranslationKeys.SetupRailStepDone,
            new TextLocalizationTranslation(LanguageCodes.English, "done"),
            new TextLocalizationTranslation(LanguageCodes.German, "erledigt"));

        Register(TranslationKeys.SetupBackToEquipmentButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Back to my equipment"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zurück zu meinen Geräten"));

        Register(TranslationKeys.SetupWelcomeNothingStartsYet,
            new TextLocalizationTranslation(LanguageCodes.English, "Nothing starts charging while you set this up. At the end you decide what may run, and you can test a real charge whenever your car is there."),
            new TextLocalizationTranslation(LanguageCodes.German, "Während der Einrichtung wird nichts geladen. Am Ende entscheiden Sie, was laufen darf, und Sie können einen echten Ladevorgang testen, sobald Ihr Auto da ist."));

        Register(TranslationKeys.SetupAccountWhatYouNeedTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "What you need"),
            new TextLocalizationTranslation(LanguageCodes.German, "Was Sie brauchen"));

        Register(TranslationKeys.SetupAccountBaseLicenceExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "A Solar4Car account with the base licence. That covers the app itself, for your whole household."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ein Solar4Car-Konto mit der Basis-Lizenz. Damit ist die App selbst abgedeckt, für Ihren gesamten Haushalt."));

        Register(TranslationKeys.SetupAccountCarSubscriptionExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Some ways of connecting a car cost extra per car. Others cost nothing beyond the base licence. We say which is which before you choose."),
            new TextLocalizationTranslation(LanguageCodes.German, "Manche Arten, ein Auto zu verbinden, kosten pro Auto extra. Andere kosten über die Basis-Lizenz hinaus nichts. Wir sagen Ihnen vor der Auswahl, was zutrifft."));

        Register(TranslationKeys.SetupLocationWhyNeeded,
            new TextLocalizationTranslation(LanguageCodes.English, "We use this to forecast how much sun you will get, and for some cars to tell whether they are parked at home."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir nutzen das, um Ihren Solarertrag vorherzusagen, und bei manchen Autos, um zu erkennen, ob sie zu Hause stehen."));

        Register(TranslationKeys.SetupPricesNoSolarNote,
            new TextLocalizationTranslation(LanguageCodes.English, "You told us you have no solar panels, so we only ask what you pay for electricity."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie haben angegeben, keine Photovoltaikanlage zu haben, daher fragen wir nur, was Sie für Strom bezahlen."));
    }

    private void RegisterEquipmentTexts()
    {
        Register(TranslationKeys.SetupEquipmentTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Your cars and charging stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihre Autos und Ladestationen"));

        Register(TranslationKeys.SetupEquipmentDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Add what you have. You can set each one up now or come back to it later."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fügen Sie hinzu, was Sie haben. Sie können jedes Gerät jetzt einrichten oder später darauf zurückkommen."));

        Register(TranslationKeys.SetupEquipmentCarsHeading,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Autos"));

        Register(TranslationKeys.SetupEquipmentNoCars,
            new TextLocalizationTranslation(LanguageCodes.English, "No cars yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch keine Autos."));

        Register(TranslationKeys.SetupEquipmentContinueCarButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Continue setting up"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einrichtung fortsetzen"));

        Register(TranslationKeys.SetupEquipmentReviewCarButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Review"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ansehen"));

        Register(TranslationKeys.SetupEquipmentRemoveCarButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Not now"),
            new TextLocalizationTranslation(LanguageCodes.German, "Jetzt nicht"));

        Register(TranslationKeys.SetupEquipmentAddCarButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add a car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto hinzufügen"));

        Register(TranslationKeys.SetupEquipmentImportTeslasButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get my cars from Tesla"),
            new TextLocalizationTranslation(LanguageCodes.German, "Meine Autos von Tesla holen"));

        Register(TranslationKeys.SetupEquipmentImportingTeslas,
            new TextLocalizationTranslation(LanguageCodes.English, "Asking Tesla which cars are on your account…"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir fragen bei Tesla nach den Autos in Ihrem Konto …"));

        Register(TranslationKeys.SetupEquipmentConnectTeslaButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect my Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Konto verbinden"));

        Register(TranslationKeys.SetupEquipmentTeslaConnectUrlError,
            new TextLocalizationTranslation(LanguageCodes.English, "We could not open the Tesla sign-in page. Please try again in a moment."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Tesla-Anmeldeseite konnte nicht geöffnet werden. Bitte versuchen Sie es gleich noch einmal."));

        Register(TranslationKeys.SetupEquipmentChargersHeading,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.SetupEquipmentChargersDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "A charging station lets us control charging for cars we cannot reach directly, and measures how much went into the car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Über eine Ladestation können wir Autos steuern, die wir nicht direkt erreichen, und messen, wie viel ins Auto geflossen ist."));

        Register(TranslationKeys.SetupEquipmentNoChargers,
            new TextLocalizationTranslation(LanguageCodes.English, "No charging stations yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch keine Ladestationen."));

        Register(TranslationKeys.SetupEquipmentContinueChargerButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Continue setting up"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einrichtung fortsetzen"));

        Register(TranslationKeys.SetupEquipmentRemoveChargerButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Not now"),
            new TextLocalizationTranslation(LanguageCodes.German, "Jetzt nicht"));

        Register(TranslationKeys.SetupEquipmentAddChargerButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add a charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestation hinzufügen"));

        Register(TranslationKeys.SetupEquipmentAddLaterHint,
            new TextLocalizationTranslation(LanguageCodes.English, "You can also carry on without any equipment and add it here whenever you are ready."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie können auch ohne Geräte fortfahren und sie hier ergänzen, sobald Sie so weit sind."));

        Register(TranslationKeys.SetupEquipmentUnnamedCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Unnamed car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Unbenanntes Auto"));

        Register(TranslationKeys.SetupEquipmentUnnamedCharger,
            new TextLocalizationTranslation(LanguageCodes.English, "Unnamed charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "Unbenannte Ladestation"));

        Register(TranslationKeys.SetupStatusActive,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging automatically"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lädt automatisch"));

        Register(TranslationKeys.SetupStatusReady,
            new TextLocalizationTranslation(LanguageCodes.English, "Ready, not switched on"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bereit, nicht aktiviert"));

        Register(TranslationKeys.SetupStatusNeedsAttention,
            new TextLocalizationTranslation(LanguageCodes.English, "Something is still missing"),
            new TextLocalizationTranslation(LanguageCodes.German, "Es fehlt noch etwas"));

        Register(TranslationKeys.SetupStatusChargerConnected,
            new TextLocalizationTranslation(LanguageCodes.English, "Connected"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbunden"));

        Register(TranslationKeys.SetupStatusChargerWaiting,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for the charger"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wartet auf die Ladestation"));
    }

    private void RegisterRouteTexts()
    {
        Register(TranslationKeys.SetupRouteNameTeslaBluetooth,
            new TextLocalizationTranslation(LanguageCodes.English, "Over Bluetooth, from a device near the car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Über Bluetooth, von einem Gerät in der Nähe des Autos"));

        Register(TranslationKeys.SetupRouteNameTeslaCloud,
            new TextLocalizationTranslation(LanguageCodes.English, "Over the internet, through your Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Über das Internet, mit Ihrem Tesla-Konto"));

        Register(TranslationKeys.SetupRouteNameSmartCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging station, with battery level from your car account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestation, Ladestand aus Ihrem Fahrzeugkonto"));

        Register(TranslationKeys.SetupRouteNameChargingStation,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging station only"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nur Ladestation"));

        Register(TranslationKeys.SetupRouteDescriptionTeslaBluetooth,
            new TextLocalizationTranslation(LanguageCodes.English, "A small device parked within a few metres of the car talks to it directly. Nothing has to go through the internet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ein kleines Gerät in wenigen Metern Entfernung spricht direkt mit dem Auto. Nichts muss über das Internet laufen."));

        Register(TranslationKeys.SetupRouteDescriptionTeslaCloud,
            new TextLocalizationTranslation(LanguageCodes.English, "We ask Tesla's own service to start, stop and adjust charging, wherever the car is."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir bitten den Dienst von Tesla, das Laden zu starten, zu stoppen und anzupassen – egal, wo das Auto steht."));

        Register(TranslationKeys.SetupRouteDescriptionSmartCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Your charging station does the charging. A connected car account tells us how full the battery is."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihre Ladestation übernimmt das Laden. Ein verbundenes Fahrzeugkonto sagt uns, wie voll der Akku ist."));

        Register(TranslationKeys.SetupRouteDescriptionChargingStation,
            new TextLocalizationTranslation(LanguageCodes.English, "Your charging station does the charging. We work out the battery level from what has been charged, or you enter it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihre Ladestation übernimmt das Laden. Den Ladestand ermitteln wir aus der geladenen Energie, oder Sie geben ihn ein."));

        Register(TranslationKeys.SetupRouteRequirementTeslaBluetooth,
            new TextLocalizationTranslation(LanguageCodes.English, "Needs: a small always-on device near the parking space, running the Bluetooth add-on."),
            new TextLocalizationTranslation(LanguageCodes.German, "Benötigt: ein kleines, dauerhaft laufendes Gerät am Stellplatz mit der Bluetooth-Erweiterung."));

        Register(TranslationKeys.SetupRouteRequirementTeslaCloud,
            new TextLocalizationTranslation(LanguageCodes.English, "Needs: internet access, your Tesla account, and a subscription for this car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Benötigt: Internetzugang, Ihr Tesla-Konto und ein Abonnement für dieses Auto."));

        Register(TranslationKeys.SetupRouteRequirementSmartCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Needs: a charging station we can control, and a subscription for this car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Benötigt: eine steuerbare Ladestation und ein Abonnement für dieses Auto."));

        Register(TranslationKeys.SetupRouteRequirementChargingStation,
            new TextLocalizationTranslation(LanguageCodes.English, "Needs: a charging station we can control. Nothing extra to pay."),
            new TextLocalizationTranslation(LanguageCodes.German, "Benötigt: eine steuerbare Ladestation. Keine zusätzlichen Kosten."));

        Register(TranslationKeys.SetupRouteControlledBy,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging is controlled through:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Laden wird gesteuert über:"));

        Register(TranslationKeys.SetupRouteBatteryLevelFrom,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery level comes from:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Ladestand kommt von:"));

        Register(TranslationKeys.SetupRouteSubscription,
            new TextLocalizationTranslation(LanguageCodes.English, "Extra subscription for this car:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zusätzliches Abonnement für dieses Auto:"));

        Register(TranslationKeys.SetupRouteControlCar,
            new TextLocalizationTranslation(LanguageCodes.English, "the car itself"),
            new TextLocalizationTranslation(LanguageCodes.German, "das Auto selbst"));

        Register(TranslationKeys.SetupRouteControlChargingStation,
            new TextLocalizationTranslation(LanguageCodes.English, "the charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "die Ladestation"));

        Register(TranslationKeys.SetupRouteBatteryBluetooth,
            new TextLocalizationTranslation(LanguageCodes.English, "the car, over Bluetooth"),
            new TextLocalizationTranslation(LanguageCodes.German, "dem Auto, über Bluetooth"));

        Register(TranslationKeys.SetupRouteBatteryTeslaAccount,
            new TextLocalizationTranslation(LanguageCodes.English, "the car, through your Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "dem Auto, über Ihr Tesla-Konto"));

        Register(TranslationKeys.SetupRouteBatterySmartCar,
            new TextLocalizationTranslation(LanguageCodes.English, "your connected car account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihrem verbundenen Fahrzeugkonto"));

        Register(TranslationKeys.SetupRouteBatteryManual,
            new TextLocalizationTranslation(LanguageCodes.English, "what you enter yourself"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihrer eigenen Eingabe"));

        Register(TranslationKeys.SetupRouteBatteryEstimated,
            new TextLocalizationTranslation(LanguageCodes.English, "an estimate from how much was charged"),
            new TextLocalizationTranslation(LanguageCodes.German, "einer Schätzung aus der geladenen Energie"));

        Register(TranslationKeys.SetupRouteBatteryEstimatedOrManual,
            new TextLocalizationTranslation(LanguageCodes.English, "an estimate from how much was charged, or what you enter yourself"),
            new TextLocalizationTranslation(LanguageCodes.German, "einer Schätzung aus der geladenen Energie oder Ihrer eigenen Eingabe"));

        Register(TranslationKeys.SetupRouteSubscriptionNone,
            new TextLocalizationTranslation(LanguageCodes.English, "none beyond the base licence"),
            new TextLocalizationTranslation(LanguageCodes.German, "keines über die Basis-Lizenz hinaus"));

        Register(TranslationKeys.SetupRouteSubscriptionRequired,
            new TextLocalizationTranslation(LanguageCodes.English, "yes, one subscription for this car"),
            new TextLocalizationTranslation(LanguageCodes.German, "ja, ein Abonnement für dieses Auto"));

        Register(TranslationKeys.SetupRouteSubscriptionsLink,
            new TextLocalizationTranslation(LanguageCodes.English, "See subscriptions and prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abonnements und Preise ansehen"));

        Register(TranslationKeys.SetupRouteNotDecidedYet,
            new TextLocalizationTranslation(LanguageCodes.English, "not decided yet"),
            new TextLocalizationTranslation(LanguageCodes.German, "noch nicht entschieden"));
    }
}
