using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class CarSettingsPageLocalizationRegistry : TextLocalizationRegistry<CarSettingsPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Cars"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge"));

        Register(TranslationKeys.CarSettingsDeleteCarTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug löschen"));

        Register(TranslationKeys.CarSettingsDeleteProgressTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting car..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug wird gelöscht..."));

        Register(TranslationKeys.CarDeletionStepChargingProcesses,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting charging history"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladehistorie wird gelöscht"));

        Register(TranslationKeys.CarDeletionStepHandledCharges,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting handled charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abgerechnete Ladevorgänge werden gelöscht"));

        Register(TranslationKeys.CarDeletionStepCarValueLogs,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting car value logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugdaten-Protokolle werden gelöscht"));

        Register(TranslationKeys.CarDeletionStepMeterValues,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting meter values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zählerwerte werden gelöscht"));

        Register(TranslationKeys.CarDeletionStepChargingTargets,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting charging targets"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeziele werden gelöscht"));

        Register(TranslationKeys.CarDeletionStepConnectorAssignments,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting charging connector assignments"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschluss-Zuordnungen werden gelöscht"));

        Register(TranslationKeys.CarDeletionStepCar,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug wird gelöscht"));

        Register(TranslationKeys.CarSettingsCreateTokenTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Fleet API Token is not valid."),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Fleet API Token ist ungültig."));

        Register(TranslationKeys.CarSettingsGoToPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "Go to "),
            new TextLocalizationTranslation(LanguageCodes.German, "Gehen Sie zu "));

        Register(TranslationKeys.CarSettingsGenerateTokenSuffix,
            new TextLocalizationTranslation(LanguageCodes.English, " and generate a Tesla Fleet API Token."),
            new TextLocalizationTranslation(LanguageCodes.German, " und generieren Sie ein Tesla Fleet API Token."));

        Register(TranslationKeys.CarSettingsAddNonTeslaButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add non-Tesla"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht-Tesla hinzufügen"));

        Register(TranslationKeys.CarSettingsCurrentBelow6AWarningTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Current below 6A not recommended"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke unter 6A nicht empfohlen"));

        Register(TranslationKeys.CarSettingsCurrentBelow6AWarningContent,
            new TextLocalizationTranslation(LanguageCodes.English, "The Type 2 standard states that the minimum current below 6A is not allowed. Setting this below 6A might result in unexpected behavior like the car not charging at all."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Typ-2-Standard besagt, dass ein Mindeststrom unter 6A nicht zulässig ist. Wenn Sie diesen Wert unter 6A einstellen, kann dies zu unerwartetem Verhalten führen, z. B. dass das Fahrzeug gar nicht lädt."));

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
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause wird erkannt, wenn das Fahrzeug meldet, 'Zuhause' zu sein."));

        Register(TranslationKeys.CarSettingsTeslaNavWorkDetectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Nav Work Detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Navi-Arbeitserkennung"));

        Register(TranslationKeys.CarSettingsTeslaNavWorkDetectionHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Home is detected if the car reports to be at 'Work'."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause wird erkannt, wenn das Fahrzeug meldet, bei der 'Arbeit' zu sein."));

        Register(TranslationKeys.CarSettingsTeslaNavFavoriteDetectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Nav Favorite Detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Navi-Favoritenerkennung"));

        Register(TranslationKeys.CarSettingsTeslaNavFavoriteDetectionHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Home is detected if the car reports to be at a 'Favorite' location. Note: This might include multiple locations."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zuhause wird erkannt, wenn das Fahrzeug meldet, an einem 'Favoriten'-Ort zu sein. Hinweis: Dies kann mehrere Orte umfassen."));

        Register(TranslationKeys.CarSettingsHomeDetectionViaLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Detection via"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimerkennung über"));

        Register(TranslationKeys.CarSettingsBleAdapterLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Bluetooth adapter"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bluetooth-Adapter"));

        Register(TranslationKeys.CarSettingsBleAdapterHelperText,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Which Bluetooth adapter of the BLE container should be used for this car. On a Raspberry Pi the onboard adapter shares its antenna with WiFi, so a USB adapter can improve the BLE connection considerably."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Welcher Bluetooth-Adapter des BLE-Containers für dieses Fahrzeug verwendet werden soll. Auf einem Raspberry Pi teilt sich der interne Adapter die Antenne mit dem WLAN, daher kann ein USB-Adapter die BLE-Verbindung deutlich verbessern."));

        Register(TranslationKeys.CarSettingsBleAdapterContainerDefault,
            new TextLocalizationTranslation(LanguageCodes.English, "Container default"),
            new TextLocalizationTranslation(LanguageCodes.German, "Standard des Containers"));

        Register(TranslationKeys.CarSettingsBleAdapterOnboard,
            new TextLocalizationTranslation(LanguageCodes.English, "Onboard"),
            new TextLocalizationTranslation(LanguageCodes.German, "Intern"));

        Register(TranslationKeys.CarSettingsBleAdapterUsb,
            new TextLocalizationTranslation(LanguageCodes.English, "USB"),
            new TextLocalizationTranslation(LanguageCodes.German, "USB"));

        Register(TranslationKeys.CarSettingsBleAdapterMissingFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} (currently not found)"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} (aktuell nicht gefunden)"));

        Register(TranslationKeys.CarSettingsBlePairingTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "BLE Pairing"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Kopplung"));

        Register(TranslationKeys.CarSettingsBlePairingHintStart,
            new TextLocalizationTranslation(LanguageCodes.English, "To use BLE commands you need to pair the TSC with your car. See "),
            new TextLocalizationTranslation(LanguageCodes.German, "Um BLE-Befehle nutzen zu können, müssen Sie TSC mit Ihrem Fahrzeug koppeln. Siehe "));

        Register(TranslationKeys.CarSettingsBlePairingLinkText,
            new TextLocalizationTranslation(LanguageCodes.English, "documentation"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dokumentation"));

        Register(TranslationKeys.CarSettingsBlePairingHintEnd,
            new TextLocalizationTranslation(LanguageCodes.English, " for more details."),
            new TextLocalizationTranslation(LanguageCodes.German, " für weitere Details."));

        Register(TranslationKeys.CarSettingsBlePairingNote,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: You need to be close to the car with your phone/key card to approve the pairing request."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Sie müssen sich mit Ihrem Telefon/Ihrer Schlüsselkarte in der Nähe des Fahrzeugs befinden, um die Kopplungsanfrage zu genehmigen."));

        Register(TranslationKeys.CarSettingsBlePairButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Pair Car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug koppeln"));

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
            new TextLocalizationTranslation(LanguageCodes.German, "Klicken Sie auf die Schaltfläche unten, um zu testen, ob TSC das Fahrzeug über BLE aufwecken kann."));

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

        Register(TranslationKeys.CarSettingsSmartCarConnectTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect to SmartCar (Car License required)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mit SmartCar verbinden (Fahrzeuglizenz erforderlich)"));

        Register(TranslationKeys.CarSettingsSmartCarDisconnectTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "Disconnect from SmartCar"),
            new TextLocalizationTranslation(LanguageCodes.German, "Von SmartCar trennen"));

        Register(TranslationKeys.CarSettingsSmartCarVinMissingError,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot connect to SmartCar: VIN is missing."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindung zu SmartCar nicht möglich: VIN fehlt."));

        Register(TranslationKeys.CarSettingsSmartCarDisconnectError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not disconnect SmartCar: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "SmartCar konnte nicht getrennt werden: {0}"));

        Register(TranslationKeys.CarSettingsSmartCarDisconnectSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "SmartCar disconnected successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "SmartCar erfolgreich getrennt."));

        Register(TranslationKeys.CarSettingsSmartCarUrlMissingError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not connect to SmartCar: URL is missing."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindung zu SmartCar nicht möglich: URL fehlt."));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add cars from Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge aus Tesla-Konto hinzufügen"));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountLoading,
            new TextLocalizationTranslation(LanguageCodes.English, "Loading cars from your Tesla account. This can take a few seconds."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge werden aus Ihrem Tesla-Konto geladen. Das kann einige Sekunden dauern."));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountNoNewCars,
            new TextLocalizationTranslation(LanguageCodes.English, "No new cars found in your Tesla account."),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine neuen Fahrzeuge in Ihrem Tesla-Konto gefunden."));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountOneCarAdded,
            new TextLocalizationTranslation(LanguageCodes.English, "1 car was added."),
            new TextLocalizationTranslation(LanguageCodes.German, "1 Fahrzeug wurde hinzugefügt."));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountCarsAdded,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} cars were added."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} Fahrzeuge wurden hinzugefügt."));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load cars from your Tesla account: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge konnten nicht aus Ihrem Tesla-Konto geladen werden: {0}"));

        Register(TranslationKeys.CarSettingsSmartCarBillingConfirmTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect to SmartCar"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mit SmartCar verbinden"));

        Register(TranslationKeys.CarSettingsSmartCarBillingConfirmText,
            new TextLocalizationTranslation(LanguageCodes.English, "In the next step you can select one or more vehicles. Any vehicle you select beyond your available car licenses is booked automatically as an additional car license and billed via Stripe (prorated). Do you want to continue?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Im nächsten Schritt können Sie ein oder mehrere Fahrzeuge auswählen. Jedes Fahrzeug, das Sie über Ihre verfügbaren Fahrzeuglizenzen hinaus auswählen, wird automatisch als zusätzliche Fahrzeuglizenz gebucht und anteilig über Stripe abgerechnet. Möchten Sie fortfahren?"));

        Register(TranslationKeys.CarSettingsSmartCarBillingConfirmButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Continue"),
            new TextLocalizationTranslation(LanguageCodes.German, "Weiter"));

        Register(TranslationKeys.AddCarDialogTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Add car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug hinzufügen"));

        Register(TranslationKeys.AddCarTokenInvalidTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Fleet API Token is not valid."),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Fleet API Token ist ungültig."));

        Register(TranslationKeys.AddCarTokenInvalidContent,
            new TextLocalizationTranslation(LanguageCodes.English, "Go to "),
            new TextLocalizationTranslation(LanguageCodes.German, "Gehen Sie zu "));

        Register(TranslationKeys.AddCarCloudConnectionLink,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.CarSettingsAddCarButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug hinzufügen"));

        Register(TranslationKeys.AddCarChooseTypeHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Choose how you want to connect your car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wählen Sie, wie Sie Ihr Fahrzeug verbinden möchten."));

        Register(TranslationKeys.AddCarFreeBadge,
            new TextLocalizationTranslation(LanguageCodes.English, "Free"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kostenlos"));

        Register(TranslationKeys.AddCarTeslaFreeBadge,
            new TextLocalizationTranslation(LanguageCodes.English, "Free with BLE"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kostenlos mit BLE"));

        Register(TranslationKeys.AddCarLicenseBadge,
            new TextLocalizationTranslation(LanguageCodes.English, "Car license required"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuglizenz erforderlich"));

        Register(TranslationKeys.AddCarManualOptionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Manual car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Manuelles Fahrzeug"));

        Register(TranslationKeys.AddCarManualOptionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Control charging without a live data connection. You set the values yourself."),
            new TextLocalizationTranslation(LanguageCodes.German, "Steuern Sie das Laden ohne Live-Datenverbindung. Sie legen die Werte selbst fest."));

        Register(TranslationKeys.AddCarTeslaOptionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla"));

        Register(TranslationKeys.AddCarTeslaOptionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Add Teslas from your connected Tesla account. Controlling them via Bluetooth (BLE) is free; using the Tesla API costs €2.99 per month."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fügen Sie Teslas aus Ihrem verbundenen Tesla-Konto hinzu. Die Steuerung über Bluetooth (BLE) ist kostenlos; die Nutzung der Tesla-API kostet 2,99 € pro Monat."));

        Register(TranslationKeys.AddCarSmartCarOptionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Other brand (SmartCar)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Andere Marke (SmartCar)"));

        Register(TranslationKeys.AddCarSmartCarOptionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect a supported vehicle via SmartCar. Consumes a car license."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbinden Sie ein unterstütztes Fahrzeug über SmartCar. Verbraucht eine Fahrzeuglizenz."));

        Register(TranslationKeys.AddCarTeslaStepHint,
            new TextLocalizationTranslation(LanguageCodes.English, "All cars found in your connected Tesla account will be added. You can remove unwanted cars afterwards."),
            new TextLocalizationTranslation(LanguageCodes.German, "Alle in Ihrem verbundenen Tesla-Konto gefundenen Fahrzeuge werden hinzugefügt. Nicht gewünschte Fahrzeuge können Sie anschließend entfernen."));

        Register(TranslationKeys.AddCarTeslaConnectHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect your Tesla account to add your Teslas. You will be redirected to Tesla to log in and your cars will be imported automatically when you return."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbinden Sie Ihr Tesla-Konto, um Ihre Teslas hinzuzufügen. Sie werden zur Anmeldung zu Tesla weitergeleitet und Ihre Fahrzeuge werden bei der Rückkehr automatisch importiert."));

        Register(TranslationKeys.AddCarConnectTeslaButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Konto verbinden"));

        Register(TranslationKeys.AddCarTeslaNeedsCloudContent,
            new TextLocalizationTranslation(LanguageCodes.English, "Connecting a Tesla requires a connected Solar4Car cloud account first. Please connect it on "),
            new TextLocalizationTranslation(LanguageCodes.German, "Zum Verbinden eines Teslas ist zunächst ein verbundenes Solar4Car-Cloud-Konto erforderlich. Bitte verbinden Sie es unter "));

        Register(TranslationKeys.AddCarTeslaNeedsCloudNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Please connect your Solar4Car cloud account before connecting a Tesla."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte verbinden Sie Ihr Solar4Car-Cloud-Konto, bevor Sie einen Tesla verbinden."));

        Register(TranslationKeys.AddCarTeslaConnectUrlError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not generate the Tesla login URL. Please try again."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Tesla-Anmelde-URL konnte nicht erzeugt werden. Bitte versuchen Sie es erneut."));

        Register(TranslationKeys.AddCarTeslaStateLoadError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load the Tesla connection state. Please try again."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Tesla-Verbindungsstatus konnte nicht geladen werden. Bitte versuchen Sie es erneut."));

        Register(TranslationKeys.AddCarRetryButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Retry"),
            new TextLocalizationTranslation(LanguageCodes.German, "Erneut versuchen"));

        Register(TranslationKeys.CarOverviewTeslaTokenExpired,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla connection expired – reconnect"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Verbindung abgelaufen – neu verbinden"));

        Register(TranslationKeys.AddCarSmartCarSearchLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Search your vehicle"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug suchen"));

        Register(TranslationKeys.AddCarSmartCarSearchHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Find your car's make, model and year to confirm it is supported by SmartCar."),
            new TextLocalizationTranslation(LanguageCodes.German, "Finden Sie Marke, Modell und Baujahr Ihres Fahrzeugs, um zu bestätigen, dass es von SmartCar unterstützt wird."));

        Register(TranslationKeys.AddCarSmartCarNotListed,
            new TextLocalizationTranslation(LanguageCodes.English, "Can't find your car? It is likely not supported by SmartCar."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Fahrzeug nicht gefunden? Es wird wahrscheinlich nicht von SmartCar unterstützt."));

        Register(TranslationKeys.AddCarSmartCarConnectButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect via SmartCar"),
            new TextLocalizationTranslation(LanguageCodes.German, "Über SmartCar verbinden"));

        Register(TranslationKeys.AddCarBackButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Back"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zurück"));

        Register(TranslationKeys.CarOverviewSmartCarConnecting,
            new TextLocalizationTranslation(LanguageCodes.English, "Connecting — waiting for first vehicle data…"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbinde — warte auf erste Fahrzeugdaten…"));

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
            new TextLocalizationTranslation(LanguageCodes.German, "Am Favoriten-Ort"));

        Register(TranslationKeys.HomeDetectionViaBlePresence,
            new TextLocalizationTranslation(LanguageCodes.English, "BLE Presence"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Anwesenheit"));

        Register(TranslationKeys.CarOverviewManaged,
            new TextLocalizationTranslation(LanguageCodes.English, "Managed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verwaltet"));

        Register(TranslationKeys.CarOverviewAmpere,
            new TextLocalizationTranslation(LanguageCodes.English, "Ampere"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ampere"));

        Register(TranslationKeys.CarOverviewPriority,
            new TextLocalizationTranslation(LanguageCodes.English, "Priority"),
            new TextLocalizationTranslation(LanguageCodes.German, "Priorität"));

        Register(TranslationKeys.CarOverviewType,
            new TextLocalizationTranslation(LanguageCodes.English, "Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Typ"));

        Register(TranslationKeys.CarOverviewBluetooth,
            new TextLocalizationTranslation(LanguageCodes.English, "Bluetooth"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bluetooth"));

        Register(TranslationKeys.CarOverviewDetection,
            new TextLocalizationTranslation(LanguageCodes.English, "Detection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Erkennung"));

        Register(TranslationKeys.CarOverviewYes,
            new TextLocalizationTranslation(LanguageCodes.English, "Yes"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ja"));

        Register(TranslationKeys.CarOverviewNo,
            new TextLocalizationTranslation(LanguageCodes.English, "No"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nein"));

        Register(TranslationKeys.CarOverviewEnabled,
            new TextLocalizationTranslation(LanguageCodes.English, "Enabled"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktiviert"));

        Register(TranslationKeys.CarOverviewDisabled,
            new TextLocalizationTranslation(LanguageCodes.English, "Disabled"),
            new TextLocalizationTranslation(LanguageCodes.German, "Deaktiviert"));

        Register(TranslationKeys.CarEditManagementSettings,
            new TextLocalizationTranslation(LanguageCodes.English, "Management Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Management-Einstellungen"));

        Register(TranslationKeys.CarEditTeslaSpecific,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla Specific"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-spezifisch"));
    }
}
