using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class SupportPageLocalizationRegistry : TextLocalizationRegistry<SupportPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.SupportPageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Support"),
            new TextLocalizationTranslation(LanguageCodes.German, "Support"));

        Register(TranslationKeys.SupportPageLoggingSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Logging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokollierung"));

        Register(TranslationKeys.SupportPageNeverShareLogsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Never share logs publicly"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gib Protokolle niemals öffentlich weiter"));

        Register(TranslationKeys.SupportPageNeverShareLogsDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Logs might contain sensitive information like your vehicle's location. Do not share logs publicly."),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokolle können sensible Informationen wie den Standort deines Fahrzeugs enthalten. Teile Protokolle nicht öffentlich."));

        Register(TranslationKeys.SupportPageDownloadServerLogsButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Download Server Logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Server-Protokolle herunterladen"));

        Register(TranslationKeys.SupportPageConfigChangeWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Do not change the configuration as this might lead to extremely high memory usage. All Settings will be reset after a restart."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ändere die Konfiguration nicht, da dies zu extrem hohem Speicherverbrauch führen kann. Alle Einstellungen werden nach einem Neustart zurückgesetzt."));

        Register(TranslationKeys.SupportPageInMemoryLogLevelLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "In Memory Log Level"),
            new TextLocalizationTranslation(LanguageCodes.German, "In-Memory-Protokollebene"));

        Register(TranslationKeys.SupportPageInMemoryLogCapacityLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "In Memory Log Capacity"),
            new TextLocalizationTranslation(LanguageCodes.German, "In-Memory-Protokollkapazität"));

        Register(TranslationKeys.SupportPageFileLogLevelLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "File Log Level"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datei-Protokollebene"));

        Register(TranslationKeys.SupportPageDownloadServerFileLogsButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Download Server File Logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Server-Dateiprotokolle herunterladen"));

        Register(TranslationKeys.SupportPageUiLogsSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "UI Logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "UI-Protokolle"));

        Register(TranslationKeys.SupportPageFetchedLogsInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "Fetched {0} logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Es wurden {0} Protokolle geladen"));

        Register(TranslationKeys.SupportPageCopyLogsButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Copy logs to clipboard"),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokolle in die Zwischenablage kopieren"));

        Register(TranslationKeys.SupportPageCarDebugDetailsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Debug Details"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeug-Debugdetails"));

        Register(TranslationKeys.SupportPageCarDebugIdFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "ID: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "ID: {0}"));

        Register(TranslationKeys.SupportPageCarDebugVinFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "VIN: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "FIN: {0}"));

        Register(TranslationKeys.SupportPageCarDebugNameFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Name: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Name: {0}"));

        Register(TranslationKeys.SupportPageCarAvailableInAccountFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Is Available in Tesla account: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Im Tesla-Konto verfügbar: {0}"));

        Register(TranslationKeys.SupportPageCarShouldBeManagedFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Should be managed: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Soll verwaltet werden: {0}"));

        Register(TranslationKeys.SupportPageResultLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Result"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebnis"));

        Register(TranslationKeys.SupportPageGetFleetTelemetryConfigButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get Fleet Telemetry Config"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fleet-Telemetrie-Konfiguration abrufen"));

        Register(TranslationKeys.SupportPageGetCarStateButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get Car State"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugstatus abrufen"));

        Register(TranslationKeys.SupportPageFleetTelemetrySetResultButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Fleet Telemetry SetResult"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fleet-Telemetrie-SetResult"));

        Register(TranslationKeys.SupportPageNormalFleetConfigurationSetButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Normal Fleet Configuration Set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Normale Fleet-Konfiguration setzen"));

        Register(TranslationKeys.SupportPageForceFleetConfigurationSetButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Force Fleet Configuration Set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fleet-Konfiguration erzwingen"));

        Register(TranslationKeys.SupportPageChargingStationDebugDetailsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging station debug details"),
            new TextLocalizationTranslation(LanguageCodes.German, "Debugdetails der Ladestation"));

        Register(TranslationKeys.SupportPageConnectorFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} Connector: {1} ({2})"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} Anschluss: {1} ({2})"));

        Register(TranslationKeys.SupportPageChargingCurrentToSetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Current to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu setzender Ladestrom"));

        Register(TranslationKeys.SupportPageChargingCurrentHelpText,
            new TextLocalizationTranslation(LanguageCodes.English, "When starting a charge or changing the current, this value will be used"),
            new TextLocalizationTranslation(LanguageCodes.German, "Beim Starten eines Ladevorgangs oder beim Ändern des Stroms wird dieser Wert verwendet."));

        Register(TranslationKeys.SupportPageChargingPhasesToSetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Phases to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu setzende Ladephasen"));

        Register(TranslationKeys.SupportPageChargingPhasesHelpText,
            new TextLocalizationTranslation(LanguageCodes.English, "When starting a charge or changing the current, this value will be used. Note: The charger might reject the request if it does not support phase switching or you enter 3 on a charger that is only connected to one phase. Leave empty to not set the value for the charger."),
            new TextLocalizationTranslation(LanguageCodes.German, "Beim Starten eines Ladevorgangs oder beim Ändern des Stroms wird dieser Wert verwendet. Hinweis: Das Ladegerät kann die Anforderung ablehnen, wenn es keine Phasenumschaltung unterstützt oder du 3 auswählst, obwohl das Ladegerät nur an eine Phase angeschlossen ist. Leer lassen, um keinen Wert für das Ladegerät zu setzen."));

        Register(TranslationKeys.SupportPageStartChargingButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Start Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgang starten"));

        Register(TranslationKeys.SupportPageStopChargingButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Stop Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladevorgang stoppen"));

        Register(TranslationKeys.SupportPageSetCurrentAndPhasesButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Current and Phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strom und Phasen setzen"));

        Register(TranslationKeys.SupportPageConfigurationKeyToGetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Configuration Key to get"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abzurufender Konfigurationsschlüssel"));

        Register(TranslationKeys.SupportPageGetConnectorStateButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get ConnectorState"),
            new TextLocalizationTranslation(LanguageCodes.German, "Anschlussstatus abrufen"));

        Register(TranslationKeys.SupportPageGetConfigurationKeyButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get Configuration Key"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurationsschlüssel abrufen"));

        Register(TranslationKeys.SupportPageSetMeterDataConfigurationButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Meter data configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Meterdatenkonfiguration setzen"));

        Register(TranslationKeys.SupportPageSetMeterIntervalConfigurationButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Meter interval Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zählerintervall-Konfiguration setzen"));

        Register(TranslationKeys.SupportPageRebootChargerButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Reboot Charger"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladegerät neu starten"));

        Register(TranslationKeys.SupportPageTriggerStatusNotificationButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Trigger Status Notification"),
            new TextLocalizationTranslation(LanguageCodes.German, "Statusbenachrichtigung auslösen"));

        Register(TranslationKeys.SupportPageCommandResultLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Command Result:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Befehlergebnis:"));

        Register(TranslationKeys.SupportPageMeterValuesButton,
            new TextLocalizationTranslation(LanguageCodes.English, "MeterValues"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zählerwerte"));

        Register(TranslationKeys.SupportPageMeterValuesWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Depending on your database size and hardware this might take a few minutes, please wait..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Je nach Datenbankgröße und Hardware kann dies einige Minuten dauern. Bitte warte..."));

        Register(TranslationKeys.SupportPageGetLatestMeterValuesButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Get latest Meter values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neueste Zählerwerte abrufen"));

        Register(TranslationKeys.SupportPageReloadPageButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Reload page"),
            new TextLocalizationTranslation(LanguageCodes.German, "Seite neu laden"));

        Register(TranslationKeys.SupportPageVinUnknownMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "VIN is unknown"),
            new TextLocalizationTranslation(LanguageCodes.German, "FIN ist unbekannt"));

        Register(TranslationKeys.SupportPageCannotCheckConfigNotInAccountMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Can not check config as car is not part of Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration kann nicht geprüft werden, da das Auto nicht Teil des Tesla-Kontos ist."));

        Register(TranslationKeys.SupportPageCannotCheckConfigUnknownVinMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Can not check config as Vin is unknown"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration kann nicht geprüft werden, da die FIN unbekannt ist."));

        Register(TranslationKeys.SupportPageCannotSetConfigNotInAccountMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Can not set config as car is not part of Tesla account"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfiguration kann nicht gesetzt werden, da das Auto nicht Teil des Tesla-Kontos ist."));

        Register(TranslationKeys.SupportPageLogLevelUpdatedMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Log level updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokollebene aktualisiert"));

        Register(TranslationKeys.SupportPageFileLogLevelUpdatedMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "File Log level updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datei-Protokollebene aktualisiert"));

        Register(TranslationKeys.SupportPageLogCapacityUpdatedMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Log capacity updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Protokollkapazität aktualisiert"));

        Register(TranslationKeys.SupportPageNoErrorMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "No error message"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Fehlermeldung"));

        Register(TranslationKeys.SupportPageNoDataMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "No data"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Daten"));

        Register(TranslationKeys.SupportPageLogLevelVerbose,
            new TextLocalizationTranslation(LanguageCodes.English, "Verbose"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ausführlich"));

        Register(TranslationKeys.SupportPageLogLevelDebug,
            new TextLocalizationTranslation(LanguageCodes.English, "Debug"),
            new TextLocalizationTranslation(LanguageCodes.German, "Debug"));

        Register(TranslationKeys.SupportPageLogLevelInformation,
            new TextLocalizationTranslation(LanguageCodes.English, "Information"),
            new TextLocalizationTranslation(LanguageCodes.German, "Information"));

        Register(TranslationKeys.SupportPageLogLevelWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Warning"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warnung"));

        Register(TranslationKeys.SupportPageLogLevelError,
            new TextLocalizationTranslation(LanguageCodes.English, "Error"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler"));

        Register(TranslationKeys.SupportPageLogLevelFatal,
            new TextLocalizationTranslation(LanguageCodes.English, "Fatal"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kritisch"));
    }
}
