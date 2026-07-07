using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class SupportPageLocalizationRegistry : TextLocalizationRegistry<SupportPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.SupportPageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Support"),
            new TextLocalizationTranslation(LanguageCodes.German, "Support"));

        Register(TranslationKeys.SupportLoggingSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Logging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokollierung"));

        Register(TranslationKeys.SupportNeverShareLogsPubliclyTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Never share logs publicly"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geben Sie Protokolle niemals öffentlich weiter"));

        Register(TranslationKeys.SupportNeverShareLogsPubliclyContent,
            new TextLocalizationTranslation(LanguageCodes.English, "Logs might contain sensitive information like your vehicle's location. Do not share logs publicly."),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokolle können sensible Informationen wie den Standort Ihres Fahrzeugs enthalten. Teilen Sie Protokolle nicht öffentlich."));

        Register(TranslationKeys.SupportDownloadServerLogsButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Download Server Logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Server-Protokolle herunterladen"));

        Register(TranslationKeys.SupportConfigurationWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Do not change the configuration as this might lead to extremely high memory usage. All Settings will be reset after a restart."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ändern Sie die Konfiguration nicht, da dies zu extrem hohem Speicherverbrauch führen kann. Alle Einstellungen werden nach einem Neustart zurückgesetzt."));

        Register(TranslationKeys.SupportInMemoryLogLevelLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "In Memory Log Level"),
            new TextLocalizationTranslation(LanguageCodes.German, "In-Memory-Protokollebene"));

        Register(TranslationKeys.SupportInMemoryLogCapacityLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "In Memory Log Capacity"),
            new TextLocalizationTranslation(LanguageCodes.German, "In-Memory-Protokollkapazität"));

        Register(TranslationKeys.SupportFileLogLevelLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "File Log Level"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datei-Protokollebene"));

        Register(TranslationKeys.SupportDownloadServerFileLogsButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Download Server File Logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Server-Dateiprotokolle herunterladen"));

        Register(TranslationKeys.SupportBleContainerLogsSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "BLE Container Logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Container-Protokolle"));

        Register(TranslationKeys.SupportBleContainerUsedByFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Used by: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verwendet von: {0}"));

        Register(TranslationKeys.SupportDownloadBleContainerLogsButtonFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Download BLE Logs ({0})"),
            new TextLocalizationTranslation(LanguageCodes.German, "BLE-Protokolle herunterladen ({0})"));

        Register(TranslationKeys.SupportUiLogsSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "UI Logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "UI-Protokolle"));

        Register(TranslationKeys.SupportFetchedLogsFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Fetched {0} logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Es wurden {0} Protokolle geladen"));

        Register(TranslationKeys.SupportCopyLogsToClipboardButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Copy logs to clipboard"),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokolle in die Zwischenablage kopieren"));

        Register(TranslationKeys.SupportCarDebugDetailsSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Debug Details"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug-Debugdetails"));

        Register(TranslationKeys.SupportIdFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "ID: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "ID: {0}"));

        Register(TranslationKeys.SupportVinFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "VIN: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "FIN: {0}"));

        Register(TranslationKeys.SupportNameFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Name: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Name: {0}"));

        Register(TranslationKeys.SupportIsAvailableInTeslaAccountFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Is Available in Tesla account: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Im Tesla-Konto verfügbar: {0}"));

        Register(TranslationKeys.SupportShouldBeManagedFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Should be managed: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Soll verwaltet werden: {0}"));

        Register(TranslationKeys.SupportResultTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Result"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebnis"));

        Register(TranslationKeys.SupportGetFleetTelemetryConfigButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get Fleet Telemetry Config"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fleet-Telemetrie-Konfiguration abrufen"));

        Register(TranslationKeys.SupportGetCarStateButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get Car State"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugstatus abrufen"));

        Register(TranslationKeys.SupportFleetTelemetrySetResultTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Fleet Telemetry SetResult"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fleet-Telemetrie-SetResult"));

        Register(TranslationKeys.SupportNormalFleetConfigurationSetButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Normal Fleet Configuration Set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Normale Fleet-Konfiguration setzen"));

        Register(TranslationKeys.SupportForceFleetConfigurationSetButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Force Fleet Configuration Set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fleet-Konfiguration erzwingen"));

        Register(TranslationKeys.SupportChargingStationDebugDetailsSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging station debug details"),
            new TextLocalizationTranslation(LanguageCodes.German, "Debugdetails der Ladestation"));

        Register(TranslationKeys.SupportConnectorFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} Connector: {1} ({2})"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} Ladeanschluss: {1} ({2})"));

        Register(TranslationKeys.SupportChargingCurrentToSetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Current to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu setzender Ladestrom"));

        Register(TranslationKeys.SupportChargingCurrentToSetHelperText,
            new TextLocalizationTranslation(LanguageCodes.English, "When starting a charge or changing the current, this value will be used"),
            new TextLocalizationTranslation(LanguageCodes.German, "Beim Starten eines Ladevorgangs oder beim Ändern des Stroms wird dieser Wert verwendet."));

        Register(TranslationKeys.SupportChargingPhasesToSetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Phases to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu setzende Ladephasen"));

        Register(TranslationKeys.SupportChargingPhasesToSetHelperText,
            new TextLocalizationTranslation(LanguageCodes.English, "When starting a charge or changing the current, this value will be used. Note: The charger might reject the request if it does not support phase switching or you enter 3 on a charger that is only connected to one phase. Leave empty to not set the value for the charger."),
            new TextLocalizationTranslation(LanguageCodes.German, "Beim Starten eines Ladevorgangs oder beim Ändern des Stroms wird dieser Wert verwendet. Hinweis: Die Ladestation kann die Anforderung ablehnen, wenn sie keine Phasenumschaltung unterstützt oder Sie 3 auswählen, obwohl die Ladestation nur an eine Phase angeschlossen ist. Leer lassen, um keinen Wert für die Ladestation zu setzen."));

        Register(TranslationKeys.SupportStartChargingButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Start Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgang starten"));

        Register(TranslationKeys.SupportStopChargingButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Stop Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgang stoppen"));

        Register(TranslationKeys.SupportSetCurrentAndPhasesButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Current and Phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strom und Phasen setzen"));

        Register(TranslationKeys.SupportConfigurationKeyToGetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Configuration Key to get"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abzurufender Konfigurationsschlüssel"));

        Register(TranslationKeys.SupportGetConnectorStateButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get ConnectorState"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschluss-Status abrufen"));

        Register(TranslationKeys.SupportGetConfigurationKeyButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get Configuration Key"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurationsschlüssel abrufen"));

        Register(TranslationKeys.SupportSetMeterDataConfigurationButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Meter Data Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zählerdatenkonfiguration setzen"));

        Register(TranslationKeys.SupportSetMeterIntervalConfigurationButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Meter Interval Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zählerintervall-Konfiguration setzen"));

        Register(TranslationKeys.SupportRebootChargerButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Reboot Charger"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestation neu starten"));

        Register(TranslationKeys.SupportTriggerStatusNotificationButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Trigger Status Notification"),
            new TextLocalizationTranslation(LanguageCodes.German, "Statusbenachrichtigung auslösen"));

        Register(TranslationKeys.SupportCommandResultTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Command Result:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Befehlsergebnis:"));

        Register(TranslationKeys.SupportMeterValuesSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "MeterValues"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zählerwerte"));

        Register(TranslationKeys.SupportMeterValuesLoadingMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Depending on your database size and hardware this might take a few minutes, please wait..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Je nach Datenbankgröße und Hardware kann dies einige Minuten dauern. Bitte warten Sie..."));

        Register(TranslationKeys.SupportGetLatestMeterValuesButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get latest Meter values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neueste Zählerwerte abrufen"));

        Register(TranslationKeys.SupportChargingPricesFrom,
            new TextLocalizationTranslation(LanguageCodes.English, "From"),
            new TextLocalizationTranslation(LanguageCodes.German, "Von"));

        Register(TranslationKeys.SupportChargingPricesTo,
            new TextLocalizationTranslation(LanguageCodes.English, "To"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bis"));

        Register(TranslationKeys.SupportReloadPageButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Reload page"),
            new TextLocalizationTranslation(LanguageCodes.German, "Seite neu laden"));

        Register(TranslationKeys.SupportClearTeslaTokenEncryptionKeyButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Clear Tesla Token Encryption Key"),
            new TextLocalizationTranslation(LanguageCodes.German, "Tesla-Token-Verschlüsselungsschlüssel zurücksetzen"));

        Register(TranslationKeys.SupportVinUnknownError,
            new TextLocalizationTranslation(LanguageCodes.English, "VIN is unknown"),
            new TextLocalizationTranslation(LanguageCodes.German, "FIN ist unbekannt"));

        Register(TranslationKeys.SupportCarNotPartOfTeslaAccountTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot check config as car is not part of Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration kann nicht geprüft werden, da das Fahrzeug nicht Teil des Tesla-Kontos ist."));

        Register(TranslationKeys.SupportVinUnknownTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot check config as VIN is unknown"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration kann nicht geprüft werden, da die FIN unbekannt ist."));

        Register(TranslationKeys.SupportCarNotPartOfTeslaAccountSetTooltip,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot set config as car is not part of Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration kann nicht gesetzt werden, da das Fahrzeug nicht Teil des Tesla-Kontos ist."));

        Register(TranslationKeys.SupportLogLevelUpdatedNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Log level updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokollebene aktualisiert"));

        Register(TranslationKeys.SupportFileLogLevelUpdatedNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "File Log level updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datei-Protokollebene aktualisiert"));

        Register(TranslationKeys.SupportLogCapacityUpdatedNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Log capacity updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokollkapazität aktualisiert"));

        Register(TranslationKeys.SupportNoError,
            new TextLocalizationTranslation(LanguageCodes.English, "No error message"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Fehlermeldung"));

        Register(TranslationKeys.SupportNoData,
            new TextLocalizationTranslation(LanguageCodes.English, "No data"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Daten"));

        Register(TranslationKeys.LogLevelVerbose,
            new TextLocalizationTranslation(LanguageCodes.English, "Verbose"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ausführlich"));

        Register(TranslationKeys.LogLevelDebug,
            new TextLocalizationTranslation(LanguageCodes.English, "Debug"),
            new TextLocalizationTranslation(LanguageCodes.German, "Debug"));

        Register(TranslationKeys.LogLevelInformation,
            new TextLocalizationTranslation(LanguageCodes.English, "Information"),
            new TextLocalizationTranslation(LanguageCodes.German, "Information"));

        Register(TranslationKeys.LogLevelWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Warning"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warnung"));

        Register(TranslationKeys.LogLevelError,
            new TextLocalizationTranslation(LanguageCodes.English, "Error"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler"));

        Register(TranslationKeys.LogLevelFatal,
            new TextLocalizationTranslation(LanguageCodes.English, "Fatal"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kritisch"));

        Register(TranslationKeys.SupportChargingPricesSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Prices"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladepreise"));

        Register(TranslationKeys.SupportGetPriceValuesButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get Price Values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Preiswerte abrufen"));

        Register(TranslationKeys.SupportHomeBatterySectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Battery Control"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeichersteuerung"));

        Register(TranslationKeys.SupportHomeBatteryNoControllersMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "No controllable home battery configured. Enable home battery control in a supported solar value source configuration (SMA Hybrid Inverter, Kostal Hybrid Inverter or Tesla Powerwall)."),
            new TextLocalizationTranslation(LanguageCodes.German, "Kein steuerbarer Heimspeicher konfiguriert. Aktivieren Sie die Heimspeichersteuerung in einer unterstützten Solarwert-Quellenkonfiguration (SMA-Hybrid-Wechselrichter, Kostal-Hybrid-Wechselrichter oder Tesla Powerwall)."));

        Register(TranslationKeys.SupportHomeBatteryCurrentModeFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Current mode: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktueller Modus: {0}"));

        Register(TranslationKeys.SupportHomeBatteryOverrideFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Manual override {0} active until {1}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Manuelle Übersteuerung {0} aktiv bis {1}"));

        Register(TranslationKeys.SupportHomeBatteryNoOverrideMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "No manual override active"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine manuelle Übersteuerung aktiv"));

        Register(TranslationKeys.SupportHomeBatterySocFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery SoC: {0} %"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie-Ladestand: {0} %"));

        Register(TranslationKeys.SupportHomeBatteryPowerFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery power: {0} W (positive = charging)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterieleistung: {0} W (positiv = lädt)"));

        Register(TranslationKeys.SupportHomeBatteryMaxChargeSocFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Max charge SoC: {0} % (charge mode is demoted to hold when reached)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Maximaler Lade-SoC: {0} % (Lademodus wird bei Erreichen auf Halten zurückgestuft)"));

        Register(TranslationKeys.SupportHomeBatteryModeToSetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Mode to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu setzender Modus"));

        Register(TranslationKeys.SupportHomeBatteryDurationLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Override duration in minutes"),
            new TextLocalizationTranslation(LanguageCodes.German, "Dauer der Übersteuerung in Minuten"));

        Register(TranslationKeys.SupportHomeBatterySetModeButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set mode"),
            new TextLocalizationTranslation(LanguageCodes.German, "Modus setzen"));

        Register(TranslationKeys.SupportHomeBatteryClearOverrideButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Clear override"),
            new TextLocalizationTranslation(LanguageCodes.German, "Übersteuerung aufheben"));

        Register(TranslationKeys.SupportHomeBatteryRefreshButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Refresh state"),
            new TextLocalizationTranslation(LanguageCodes.German, "Status aktualisieren"));

        Register(TranslationKeys.SupportHomeBatteryModeSetNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Home battery mode set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeichermodus gesetzt"));

        Register(TranslationKeys.SupportHomeBatteryOverrideClearedNotification,
            new TextLocalizationTranslation(LanguageCodes.English, "Manual override cleared"),
            new TextLocalizationTranslation(LanguageCodes.German, "Manuelle Übersteuerung aufgehoben"));

        Register(TranslationKeys.SupportHomeBatteryPeriodicRewriteText,
            new TextLocalizationTranslation(LanguageCodes.English, "Setpoints are rewritten periodically"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sollwerte werden periodisch neu geschrieben"));

        Register(TranslationKeys.SupportHomeBatteryLastWriteFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Last successful write: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Letzter erfolgreicher Schreibvorgang: {0}"));

        Register(TranslationKeys.SupportHomeBatteryLastErrorFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Last error: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Letzter Fehler: {0}"));

        Register(TranslationKeys.SupportHomeBatteryValidationHint,
            new TextLocalizationTranslation(LanguageCodes.English, "To validate the configuration set hold mode while the battery is discharging (battery power should go to about 0 W) or charge mode (battery power should go to about the configured max charge power). The override automatically expires after the configured duration and normal mode is restored."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zur Überprüfung der Konfiguration setzen Sie den Halten-Modus, während die Batterie entlädt (Batterieleistung sollte auf ca. 0 W gehen), oder den Lade-Modus (Batterieleistung sollte auf ca. die konfigurierte maximale Ladeleistung gehen). Die Übersteuerung läuft nach der konfigurierten Dauer automatisch ab und der Normalmodus wird wiederhergestellt."));

        Register(TranslationKeys.HomeBatteryModeUnknown,
            new TextLocalizationTranslation(LanguageCodes.English, "Unknown (not modified by TSC)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Unbekannt (nicht durch TSC verändert)"));

        Register(TranslationKeys.HomeBatteryModeNormal,
            new TextLocalizationTranslation(LanguageCodes.English, "Normal"),
            new TextLocalizationTranslation(LanguageCodes.German, "Normal"));

        Register(TranslationKeys.HomeBatteryModeHold,
            new TextLocalizationTranslation(LanguageCodes.English, "Hold (block discharging)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Halten (Entladen blockieren)"));

        Register(TranslationKeys.HomeBatteryModeCharge,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge (force charging)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden (Laden erzwingen)"));
    }
}
