using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public partial class SetupPageLocalizationRegistry : TextLocalizationRegistry<SetupPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.SetupAssistantTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Setup Assistant"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einrichtungsassistent"));

        Register(TranslationKeys.SetupStepWelcome,
            new TextLocalizationTranslation(LanguageCodes.English, "Welcome"),
            new TextLocalizationTranslation(LanguageCodes.German, "Willkommen"));

        Register(TranslationKeys.SetupStepLocation,
            new TextLocalizationTranslation(LanguageCodes.English, "Location"),
            new TextLocalizationTranslation(LanguageCodes.German, "Standort"));

        Register(TranslationKeys.SetupStepSolarBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar & Battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar & Batterie"));

        Register(TranslationKeys.SetupStepCloudConnection,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.SetupStepPrices,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register(TranslationKeys.SetupStepCars,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge"));

        Register(TranslationKeys.SetupStepChargingStations,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.SetupStepCarsAndCharging,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars & Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge & Laden"));

        Register(TranslationKeys.SetupStepFinish,
            new TextLocalizationTranslation(LanguageCodes.English, "Finish"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abschluss"));

        Register(TranslationKeys.SetupWelcomeTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Welcome to TeslaSolarCharger!"),
            new TextLocalizationTranslation(LanguageCodes.German, "Willkommen bei TeslaSolarCharger!"));

        Register(TranslationKeys.SetupWelcomeDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "This assistant will guide you through the basic configuration to get you started as quickly as possible."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieser Assistent führt Sie durch die Basiskonfiguration, um Ihnen den Einstieg so einfach wie möglich zu machen."));

        Register(TranslationKeys.SetupChangeLaterInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "You can always change these settings later in the configuration pages."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie können diese Einstellungen jederzeit später in den Konfigurationsseiten ändern."));

        Register(TranslationKeys.SetupHasPvSystemQuestion,
            new TextLocalizationTranslation(LanguageCodes.English, "Do you have a photovoltaic (PV) system?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Haben Sie eine Photovoltaikanlage (PV)?"));

        Register(TranslationKeys.SetupLocationTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "System Location"),
            new TextLocalizationTranslation(LanguageCodes.German, "Standort der Anlage"));

        Register(TranslationKeys.SetupLocationDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Please set the location of your PV system on the map. This is used for solar power predictions. For certain vehicle models, this location is also used to determine whether the vehicle is at home."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte legen Sie den Standort Ihrer PV-Anlage auf der Karte fest. Dies wird für Solarstromprognosen verwendet. Bei bestimmten Fahrzeugmodellen wird dieser Standort außerdem verwendet, um festzustellen, ob sich das Fahrzeug zu Hause befindet."));

        Register(TranslationKeys.SetupSolarBatteryTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar & Battery Data"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar- & Batteriedaten"));

        Register(TranslationKeys.SetupSolarBatteryDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Configure how TeslaSolarCharger gets your current solar production and grid usage data. Templates are the easiest way to start."),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurieren Sie, wie TeslaSolarCharger Ihre aktuelle Solarproduktion und den Netzbezug ermittelt. Vorlagen sind der einfachste Weg zum Starten."));

        Register(TranslationKeys.SetupPvNotAvailableAsTemplate,
            new TextLocalizationTranslation(LanguageCodes.English, "My PV system is not available as a template"),
            new TextLocalizationTranslation(LanguageCodes.German, "Meine PV-Anlage ist nicht als Vorlage verfügbar"));

        Register(TranslationKeys.SetupHasHomeBatteryQuestion,
            new TextLocalizationTranslation(LanguageCodes.English, "Do you have a home battery?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Haben Sie eine Heimbatterie?"));

        Register(TranslationKeys.SetupHomeBatterySettingsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Battery Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimbatterie-Einstellungen"));

        Register(TranslationKeys.SetupCloudConnectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar4Car Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar4Car-Cloud-Verbindung"));

        Register(TranslationKeys.SetupCloudConnectionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "A Solar4Car account is required to use TeslaSolarCharger. Please log in or create an account to continue. You cannot proceed until your instance is connected."),
            new TextLocalizationTranslation(LanguageCodes.German, "Für die Nutzung von TeslaSolarCharger ist ein Solar4Car-Konto erforderlich. Bitte melden Sie sich an oder erstellen Sie ein Konto, um fortzufahren. Sie können erst fortfahren, wenn Ihre Instanz verbunden ist."));

        Register(TranslationKeys.SetupCloudConnectionRequiredNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Please connect your instance to the Solar4Car cloud before continuing."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte verbinden Sie Ihre Instanz mit der Solar4Car-Cloud, bevor Sie fortfahren."));

        Register(TranslationKeys.SetupBaseAppLicenseRequiredNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "A Solar4Car base license is required before continuing."),
            new TextLocalizationTranslation(LanguageCodes.German, "Für die Fortsetzung ist eine Solar4Car-Basislizenz erforderlich."));

        Register(TranslationKeys.SetupBaseAppLicenseMissingInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "Your account is connected, but it does not include a base license. A base license is required to use TeslaSolarCharger. You can purchase one here:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Konto ist verbunden, enthält aber keine Basislizenz. Für die Nutzung von TeslaSolarCharger ist eine Basislizenz erforderlich. Sie können diese hier erwerben:"));

        Register(TranslationKeys.SetupBaseAppLicenseRecheckButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Re-check license"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lizenz erneut prüfen"));

        Register(TranslationKeys.SetupPricesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Electricity Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strompreise"));

        Register(TranslationKeys.SetupPricesDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Configure your electricity prices to allow TeslaSolarCharger to charge when it's cheapest."),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurieren Sie Ihre Strompreise, damit TeslaSolarCharger laden kann, wenn es am günstigsten ist."));

        Register(TranslationKeys.SetupCarsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge"));

        Register(TranslationKeys.SetupChargingStationsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.SetupChargingStationsDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "As soon as a charging station connects via OCPP, it will appear here in real-time."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sobald sich eine Ladestation über OCPP verbindet, erscheint sie hier in Echtzeit."));

        Register(TranslationKeys.SetupNoStationsConnected,
            new TextLocalizationTranslation(LanguageCodes.English, "No charging stations connected yet..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch keine Ladestationen verbunden..."));

        Register(TranslationKeys.SetupFinishTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Almost done!"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fast fertig!"));

        Register(TranslationKeys.SetupFinishDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Click \"Finish Setup\" to save your configuration and start using TeslaSolarCharger."),
            new TextLocalizationTranslation(LanguageCodes.German, "Klicken Sie auf \"Einrichtung abschließen\", um Ihre Konfiguration zu speichern und TeslaSolarCharger zu nutzen."));

        Register(TranslationKeys.SetupSuccessInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "Your initial setup is complete. You can always revisit the detailed settings pages for fine-tuning."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihre Ersteinrichtung ist abgeschlossen. Sie können die detaillierten Einstellungsseiten jederzeit für die Feinabstimmung besuchen."));

        Register(TranslationKeys.SetupFinishButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Finish Setup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einrichtung abschließen"));

        Register(TranslationKeys.SetupSuccessNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Setup completed successfully!"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einrichtung erfolgreich abgeschlossen!"));

        RegisterDecisionTexts();
        RegisterGuidedFlowTexts();
        RegisterSolarAndBatteryTexts();
        RegisterPriceTexts();
    }

    /// <summary>
    /// Texts the setup decision service refers to by key. They say what is still missing or why something was
    /// decided automatically, without naming internal settings.
    /// </summary>
    private void RegisterDecisionTexts()
    {
        Register(TranslationKeys.SetupIssuePvQuestionUnanswered,
            new TextLocalizationTranslation(LanguageCodes.English, "Tell us whether you have solar panels."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sagen Sie uns, ob Sie eine Photovoltaikanlage haben."));

        Register(TranslationKeys.SetupIssueHomeBatteryQuestionUnanswered,
            new TextLocalizationTranslation(LanguageCodes.English, "Tell us whether you have a home battery."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sagen Sie uns, ob Sie einen Hausspeicher haben."));

        Register(TranslationKeys.SetupIssueGridPowerSourceMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "We cannot read yet how much electricity you send to or take from the grid. Connect your inverter or meter."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir können noch nicht ablesen, wie viel Strom Sie ins Netz einspeisen oder daraus beziehen. Verbinden Sie Ihren Wechselrichter oder Zähler."));

        Register(TranslationKeys.SetupIssueHomeBatterySourceMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "We cannot read your home battery's charge level and power yet. Connect the battery as a data source."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir können den Ladestand und die Leistung Ihres Hausspeichers noch nicht ablesen. Verbinden Sie den Speicher als Datenquelle."));

        Register(TranslationKeys.SetupIssueHomeBatteryCapacityUnknown,
            new TextLocalizationTranslation(LanguageCodes.English, "We need your home battery's usable capacity in kWh. You will find it in the battery's data sheet or app."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir benötigen die nutzbare Kapazität Ihres Hausspeichers in kWh. Sie finden sie im Datenblatt oder in der App des Speichers."));

        Register(TranslationKeys.SetupIssueHomeBatteryChargingPowerUnknown,
            new TextLocalizationTranslation(LanguageCodes.English, "We need the power in watts your home battery can charge with. You will find it in the battery's data sheet or app."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir benötigen die Leistung in Watt, mit der Ihr Hausspeicher laden kann. Sie finden sie im Datenblatt oder in der App des Speichers."));

        Register(TranslationKeys.SetupIssueHomeLocationNotConfirmed,
            new TextLocalizationTranslation(LanguageCodes.English, "Confirm where your cars charge. The preset location on the map is only an example, not your address."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bestätigen Sie, wo Ihre Autos laden. Der voreingestellte Ort auf der Karte ist nur ein Beispiel, nicht Ihre Adresse."));

        Register(TranslationKeys.SetupIssueGridPriceMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "Enter what you pay for a kilowatt hour of electricity."),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie an, was Sie für eine Kilowattstunde Strom bezahlen."));

        Register(TranslationKeys.SetupIssueCloudConnectionMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "Sign in with your Solar4Car account."),
            new TextLocalizationTranslation(LanguageCodes.German, "Melden Sie sich mit Ihrem Solar4Car-Konto an."));

        Register(TranslationKeys.SetupIssueBaseAppLicenseMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "Your Solar4Car account still needs the base licence."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihrem Solar4Car-Konto fehlt noch die Basis-Lizenz."));

        Register(TranslationKeys.SetupIssueNoEquipmentConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "Add a car or a charging station, or choose to add your equipment later."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fügen Sie ein Auto oder eine Ladestation hinzu, oder ergänzen Sie Ihre Geräte später."));

        Register(TranslationKeys.SetupIssueCarConnectionRouteUndecided,
            new TextLocalizationTranslation(LanguageCodes.English, "Choose how this car should be controlled."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wählen Sie, wie dieses Auto gesteuert werden soll."));

        Register(TranslationKeys.SetupIssueCarNameMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "Give this car a name so you can tell it apart from your other cars."),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie diesem Auto einen Namen, damit Sie es von Ihren anderen Autos unterscheiden können."));

        Register(TranslationKeys.SetupIssueCarVinMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "We need this car's vehicle identification number. You will find it in the car's app or on the windscreen."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir benötigen die Fahrgestellnummer dieses Autos. Sie finden sie in der App des Autos oder an der Windschutzscheibe."));

        Register(TranslationKeys.SetupIssueCarUsableEnergyUnknown,
            new TextLocalizationTranslation(LanguageCodes.English, "We need this car's usable battery capacity in kWh to plan charging. You will find it in the car's data sheet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir benötigen die nutzbare Akkukapazität dieses Autos in kWh, um das Laden zu planen. Sie finden sie im Datenblatt des Autos."));

        Register(TranslationKeys.SetupIssueCarMaximumPhasesUnknown,
            new TextLocalizationTranslation(LanguageCodes.English, "Tell us whether this car charges on one or on three phases."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sagen Sie uns, ob dieses Auto einphasig oder dreiphasig lädt."));

        Register(TranslationKeys.SetupIssueCarCurrentLimitsInvalid,
            new TextLocalizationTranslation(LanguageCodes.English, "This car's highest charging current must not be below its lowest charging current."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der höchste Ladestrom dieses Autos darf nicht unter seinem niedrigsten Ladestrom liegen."));

        Register(TranslationKeys.SetupIssueCarBleApiUrlMissing,
            new TextLocalizationTranslation(LanguageCodes.English, "Tell us how to reach the Bluetooth device that sits near this car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sagen Sie uns, wie das Bluetooth-Gerät in der Nähe dieses Autos erreichbar ist."));

        Register(TranslationKeys.SetupIssueTeslaAccountNotConnected,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect your Tesla account so we can control this car over the internet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbinden Sie Ihr Tesla-Konto, damit wir dieses Auto über das Internet steuern können."));

        Register(TranslationKeys.SetupIssueTeslaMateConflictsWithFleetTelemetry,
            new TextLocalizationTranslation(LanguageCodes.English, "TeslaMate is selected as the source of your car data, so this car cannot stream its data to us as well. Pick one of the two."),
            new TextLocalizationTranslation(LanguageCodes.German, "TeslaMate ist als Quelle Ihrer Fahrzeugdaten ausgewählt, daher kann dieses Auto seine Daten nicht zusätzlich an uns senden. Entscheiden Sie sich für eine der beiden Quellen."));

        Register(TranslationKeys.SetupIssueCarSmartCarNotConnected,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect this car's account so we can read how full its battery is."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbinden Sie das Konto dieses Autos, damit wir seinen Ladestand auslesen können."));

        Register(TranslationKeys.SetupIssueCarNeedsChargingStation,
            new TextLocalizationTranslation(LanguageCodes.English, "This car is charged by a charging station, so you need to add one before it can charge automatically."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Auto wird über eine Ladestation geladen. Fügen Sie eine hinzu, damit es automatisch laden kann."));

        Register(TranslationKeys.SetupIssueChargingStationNotConnected,
            new TextLocalizationTranslation(LanguageCodes.English, "Your charging station has not reported in yet. Enter the connection address in its settings and wait for it to appear here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihre Ladestation hat sich noch nicht gemeldet. Tragen Sie die Verbindungsadresse in ihren Einstellungen ein und warten Sie, bis sie hier erscheint."));

        Register(TranslationKeys.SetupIssueChargingStationConnectorNotChosen,
            new TextLocalizationTranslation(LanguageCodes.English, "This charging station has several connectors. Choose the one your car is plugged into."),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Ladestation hat mehrere Anschlüsse. Wählen Sie den aus, an dem Ihr Auto angeschlossen ist."));

        Register(TranslationKeys.SetupIssueSolarPredictionRequired,
            new TextLocalizationTranslation(LanguageCodes.English, "Working out the reserve automatically needs the solar forecast, so please switch it on as well."),
            new TextLocalizationTranslation(LanguageCodes.German, "Für die automatische Reserve wird die Solarvorhersage benötigt, schalten Sie sie daher bitte ebenfalls ein."));

        Register(TranslationKeys.SetupReasonDynamicHomeBatteryMinSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "We keep just enough charge in your home battery for the evening and let your car use the rest."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir halten genau so viel Ladung im Hausspeicher zurück, wie Sie abends brauchen, und überlassen den Rest Ihrem Auto."));

        Register(TranslationKeys.SetupReasonPredictSolarPowerGenerationForBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "We forecast the solar power at your location because working out your home battery's reserve depends on it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir sagen den Solarertrag an Ihrem Standort voraus, weil die Berechnung der Reserve Ihres Hausspeichers darauf angewiesen ist."));

        Register(TranslationKeys.SetupReasonPredictSolarPowerGeneration,
            new TextLocalizationTranslation(LanguageCodes.English, "We forecast tomorrow's solar power for your location so charging can be planned ahead."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir sagen den Solarertrag an Ihrem Standort voraus, damit das Laden vorausschauend geplant werden kann."));

        Register(TranslationKeys.SetupReasonUsePredictedSolarForSchedules,
            new TextLocalizationTranslation(LanguageCodes.English, "We use that forecast when planning when your car charges."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir nutzen diese Vorhersage bei der Planung, wann Ihr Auto lädt."));

        Register(TranslationKeys.SetupReasonShowEnergyDataOnHome,
            new TextLocalizationTranslation(LanguageCodes.English, "We show your energy figures on the start page because we can measure them."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir zeigen Ihre Energiewerte auf der Startseite an, weil wir sie messen können."));

        Register(TranslationKeys.SetupReasonGetVehicleDataViaBle,
            new TextLocalizationTranslation(LanguageCodes.English, "At least one car is set up over Bluetooth, so we read battery levels over Bluetooth too."),
            new TextLocalizationTranslation(LanguageCodes.German, "Mindestens ein Auto ist über Bluetooth eingerichtet, daher lesen wir auch den Ladestand über Bluetooth."));

        Register(TranslationKeys.SetupReasonCarHomeDetectionViaBlePresence,
            new TextLocalizationTranslation(LanguageCodes.English, "We notice this car is home when the Bluetooth device near your parking space can hear it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir erkennen, dass dieses Auto zu Hause ist, wenn das Bluetooth-Gerät an Ihrem Stellplatz es hört."));

        Register(TranslationKeys.SetupReasonCarHomeDetectionViaLocatedAtHome,
            new TextLocalizationTranslation(LanguageCodes.English, "We let Tesla tell us this car is home, because you chose not to share its exact position with us."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wir lassen uns von Tesla sagen, dass dieses Auto zu Hause ist, da Sie uns seine genaue Position nicht mitteilen möchten."));

        Register(TranslationKeys.SetupReasonCarChargingPriority,
            new TextLocalizationTranslation(LanguageCodes.English, "With a single car there is nothing to prioritise, so we set the order for you."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bei einem einzigen Auto gibt es nichts zu priorisieren, daher legen wir die Reihenfolge für Sie fest."));

        Register(TranslationKeys.SetupNextActionConnectCloud,
            new TextLocalizationTranslation(LanguageCodes.English, "Sign in with your Solar4Car account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mit Solar4Car-Konto anmelden"));

        Register(TranslationKeys.SetupNextActionDescribeSolarAndBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Tell us about your solar panels and home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Erzählen Sie uns von Ihrer Photovoltaikanlage und Ihrem Hausspeicher"));

        Register(TranslationKeys.SetupNextActionConfirmLocation,
            new TextLocalizationTranslation(LanguageCodes.English, "Confirm where your cars charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bestätigen Sie, wo Ihre Autos laden"));

        Register(TranslationKeys.SetupNextActionEnterPrices,
            new TextLocalizationTranslation(LanguageCodes.English, "Enter your electricity price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie Ihren Strompreis ein"));

        Register(TranslationKeys.SetupNextActionAddEquipment,
            new TextLocalizationTranslation(LanguageCodes.English, "Add your cars and charging stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fügen Sie Ihre Autos und Ladestationen hinzu"));

        Register(TranslationKeys.SetupNextActionCompleteCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Finish setting up this car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schließen Sie die Einrichtung dieses Autos ab"));

        Register(TranslationKeys.SetupNextActionFinish,
            new TextLocalizationTranslation(LanguageCodes.English, "Review your setup and finish"),
            new TextLocalizationTranslation(LanguageCodes.German, "Prüfen Sie Ihre Einrichtung und schließen Sie ab"));

        Register(TranslationKeys.SetupProposalsHeading,
            new TextLocalizationTranslation(LanguageCodes.English, "We will manage these settings automatically"),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Einstellungen übernehmen wir automatisch"));

        Register(TranslationKeys.SetupProposalPendingPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wartet auf:"));

        Register(TranslationKeys.SetupMissingInformationHeading,
            new TextLocalizationTranslation(LanguageCodes.English, "Still missing"),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch offen"));

        Register(TranslationKeys.SetupIncompatibilitiesHeading,
            new TextLocalizationTranslation(LanguageCodes.English, "These choices contradict each other"),
            new TextLocalizationTranslation(LanguageCodes.German, "Diese Angaben widersprechen sich"));

        Register(TranslationKeys.SetupNextActionHeading,
            new TextLocalizationTranslation(LanguageCodes.English, "Next step"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nächster Schritt"));

        Register(TranslationKeys.SetupSaveWithoutEnablingButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Save and enable later"),
            new TextLocalizationTranslation(LanguageCodes.German, "Speichern und später aktivieren"));

        Register(TranslationKeys.SetupSaveWithoutEnablingHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Keeps everything you entered and changes nothing about how your cars charge today."),
            new TextLocalizationTranslation(LanguageCodes.German, "Behält alle Ihre Eingaben und ändert nichts daran, wie Ihre Autos heute laden."));

        Register(TranslationKeys.SetupFinishBlockedHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Something is still missing, so finishing is not possible yet. The list above says what, and saving without enabling keeps your answers in the meantime."),
            new TextLocalizationTranslation(LanguageCodes.German, "Es fehlt noch etwas, daher ist das Abschließen noch nicht möglich. Die Liste oben nennt was, und Speichern ohne Aktivieren bewahrt Ihre Eingaben so lange auf."));

        Register(TranslationKeys.SetupEnableExplanation,
            new TextLocalizationTranslation(LanguageCodes.English, "Finishing lets TeslaSolarCharger control charging as soon as the conditions you set are met."),
            new TextLocalizationTranslation(LanguageCodes.German, "Mit dem Abschließen darf TeslaSolarCharger das Laden steuern, sobald die von Ihnen gesetzten Bedingungen erfüllt sind."));

        Register(TranslationKeys.SetupSavedWithoutEnablingNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved. Nothing has been switched on yet - come back here when you are ready."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert. Es wurde noch nichts aktiviert – kommen Sie zurück, wenn Sie so weit sind."));

        Register(TranslationKeys.SetupPartialSaveFailure,
            new TextLocalizationTranslation(LanguageCodes.English, "Not everything could be saved. Your answers were kept, so you can try again."),
            new TextLocalizationTranslation(LanguageCodes.German, "Es konnte nicht alles gespeichert werden. Ihre Angaben bleiben erhalten, Sie können es erneut versuchen."));

        Register(TranslationKeys.SetupConfigurationIncompleteInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "You can finish anyway. Anything still missing is listed above and can be completed later."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie können trotzdem abschließen. Was noch fehlt, steht oben und lässt sich später ergänzen."));
    }
}
