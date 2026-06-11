using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class CarSettingsPageLocalizationRegistry : TextLocalizationRegistry<CarSettingsPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarSettingsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

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

        Register(TranslationKeys.CarSettingsSmartCarConnectTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect to Smart Car (Car License required)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mit Smart Car verbinden (Fahrzeuglizenz erforderlich)"));

        Register(TranslationKeys.CarSettingsSmartCarDisconnectTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "Disconnect from Smart Car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Von Smart Car trennen"));

        Register(TranslationKeys.CarSettingsSmartCarVinMissingError,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot connect to Smart Car: VIN is missing."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindung zu Smart Car nicht möglich: VIN fehlt."));

        Register(TranslationKeys.CarSettingsSmartCarDisconnectError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not disconnect Smart Car: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Smart Car konnte nicht getrennt werden: {0}"));

        Register(TranslationKeys.CarSettingsSmartCarDisconnectSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "Smart Car disconnected successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Smart Car erfolgreich getrennt."));

        Register(TranslationKeys.CarSettingsSmartCarUrlMissingError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not connect to Smart Car: URL is missing."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbindung zu Smart Car nicht möglich: URL fehlt."));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add cars from Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge aus Tesla-Konto hinzufügen"));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "Checked your Tesla account for new cars."),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Konto auf neue Fahrzeuge geprüft."));

        Register(TranslationKeys.CarSettingsAddTeslaFromAccountError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load cars from your Tesla account: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuge konnten nicht aus Ihrem Tesla-Konto geladen werden: {0}"));

        Register(TranslationKeys.CarSettingsSmartCarBillingConfirmTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect to Smart Car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Mit Smart Car verbinden"));

        Register(TranslationKeys.CarSettingsSmartCarBillingConfirmText,
            new TextLocalizationTranslation(LanguageCodes.English, "In the next step you can select one or more vehicles. Any vehicle you select beyond your available car licenses is booked automatically as an additional car license and billed via Stripe (prorated). Do you want to continue?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Im nächsten Schritt können Sie ein oder mehrere Fahrzeuge auswählen. Jedes Fahrzeug, das Sie über Ihre verfügbaren Fahrzeuglizenzen hinaus auswählen, wird automatisch als zusätzliche Fahrzeuglizenz gebucht und anteilig über Stripe abgerechnet. Möchten Sie fortfahren?"));

        Register(TranslationKeys.CarSettingsSmartCarBillingConfirmButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Continue"),
            new TextLocalizationTranslation(LanguageCodes.German, "Weiter"));

        Register(TranslationKeys.AddCarDialogTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Add car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto hinzufügen"));

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
            new TextLocalizationTranslation(LanguageCodes.German, "Auto hinzufügen"));

        Register(TranslationKeys.AddCarChooseTypeHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Choose how you want to connect your car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wählen Sie, wie Sie Ihr Auto verbinden möchten."));

        Register(TranslationKeys.AddCarFreeBadge,
            new TextLocalizationTranslation(LanguageCodes.English, "Free"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kostenlos"));

        Register(TranslationKeys.AddCarLicenseBadge,
            new TextLocalizationTranslation(LanguageCodes.English, "Car license required"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuglizenz erforderlich"));

        Register(TranslationKeys.AddCarManualOptionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Manual car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Manuelles Auto"));

        Register(TranslationKeys.AddCarManualOptionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Control charging without a live data connection. You set the values yourself."),
            new TextLocalizationTranslation(LanguageCodes.German, "Steuern Sie das Laden ohne Live-Datenverbindung. Sie legen die Werte selbst fest."));

        Register(TranslationKeys.AddCarTeslaOptionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Tesla (Fleet Telemetry)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla (Fleet Telemetry)"));

        Register(TranslationKeys.AddCarTeslaOptionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Add Teslas from your connected Tesla account and stream their data via Fleet Telemetry."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fügen Sie Teslas aus Ihrem verbundenen Tesla-Konto hinzu und streamen Sie deren Daten über Fleet Telemetry."));

        Register(TranslationKeys.AddCarSmartCarOptionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Other brand (Smart Car)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Andere Marke (Smart Car)"));

        Register(TranslationKeys.AddCarSmartCarOptionDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect a supported vehicle via Smart Car. Consumes a car license."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbinden Sie ein unterstütztes Fahrzeug über Smart Car. Verbraucht eine Fahrzeuglizenz."));

        Register(TranslationKeys.AddCarTeslaStepHint,
            new TextLocalizationTranslation(LanguageCodes.English, "All cars found in your connected Tesla account will be added. You can remove unwanted cars afterwards."),
            new TextLocalizationTranslation(LanguageCodes.German, "Alle in Ihrem verbundenen Tesla-Konto gefundenen Autos werden hinzugefügt. Nicht gewünschte Autos können Sie anschließend entfernen."));

        Register(TranslationKeys.AddCarSmartCarSearchLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Search your vehicle"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug suchen"));

        Register(TranslationKeys.AddCarSmartCarSearchHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Find your car's make, model and year to confirm it is supported by Smart Car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Finden Sie Marke, Modell und Baujahr Ihres Autos, um zu bestätigen, dass es von Smart Car unterstützt wird."));

        Register(TranslationKeys.AddCarSmartCarNotListed,
            new TextLocalizationTranslation(LanguageCodes.English, "Can't find your car? It is likely not supported by Smart Car."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Auto nicht gefunden? Es wird wahrscheinlich nicht von Smart Car unterstützt."));

        Register(TranslationKeys.AddCarSmartCarConnectButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Connect via Smart Car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Über Smart Car verbinden"));

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
            new TextLocalizationTranslation(LanguageCodes.German, "An Favoriten"));

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
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla Spezifisch"));
    }
}
