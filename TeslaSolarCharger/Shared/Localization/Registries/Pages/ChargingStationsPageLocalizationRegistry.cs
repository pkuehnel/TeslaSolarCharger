using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class ChargingStationsPageLocalizationRegistry : TextLocalizationRegistry<ChargingStationsPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingStationsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.ChargingStationsHowToConnectTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "How to connect a new Charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "So verbinden Sie eine neue Ladestation"));

        Register(TranslationKeys.ChargingStationsHowToConnectContent,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging stations are added automatically as soon as they connect via OCPP."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen werden automatisch hinzugefügt, sobald sie sich über OCPP verbinden."));

        Register(TranslationKeys.ChargingStationsHowToConnectUrlFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "To connect, set the OCPP URL to the following: <code>ws://YOUR-TSC-IP:7190/api/Ocpp/</code> followed by a charging point ID."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zum Verbinden setzen Sie die OCPP-URL wie folgt: <code>ws://IHRE-TSC-IP:7190/api/Ocpp/</code> gefolgt von einer Ladepunkt-ID."));

        Register(TranslationKeys.ChargingStationsHowToConnectNote,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: Many charging stations automatically add a charging point ID to the URL, just make sure that the resulting URL looks similar to the following example. Mind the single <code>/</code> after <code>Ocpp</code>"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Viele Ladestationen fügen automatisch eine Ladepunkt-ID zur URL hinzu. Achten Sie darauf, dass die resultierende URL wie im folgenden Beispiel aussieht. Beachten Sie den einzelnen <code>/</code> nach <code>Ocpp</code>"));

        Register(TranslationKeys.ChargingStationsHowToConnectExample,
            new TextLocalizationTranslation(LanguageCodes.English, "<code>ws://192.168.178.36:7190/api/Ocpp/C00485L</code>"),
            new TextLocalizationTranslation(LanguageCodes.German, "<code>ws://192.168.178.36:7190/api/Ocpp/C00485L</code>"));

        Register(TranslationKeys.ChargingStationsNoStationsFound,
            new TextLocalizationTranslation(LanguageCodes.English, "No charging stations found"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Ladestationen gefunden"));

        Register(TranslationKeys.ChargingStationsConnectedViaOcpp,
            new TextLocalizationTranslation(LanguageCodes.English, "Connected via OCPP"),
            new TextLocalizationTranslation(LanguageCodes.German, "Über OCPP verbunden"));

        Register(TranslationKeys.ChargingStationsNotConnectedViaOcpp,
            new TextLocalizationTranslation(LanguageCodes.English, "Not connected via OCPP"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht über OCPP verbunden"));

        Register(TranslationKeys.ChargingStationsEditStationTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Edit charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestation bearbeiten"));

        Register(TranslationKeys.ChargingStationsDeleteStationTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestation löschen"));

        Register(TranslationKeys.ChargingStationDeleteProgressTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting charging station..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestation wird gelöscht..."));

        Register(TranslationKeys.ChargingStationDeletionStepChargingProcesses,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting charging history"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladehistorie wird gelöscht"));

        Register(TranslationKeys.ChargingStationDeletionStepTransactions,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting OCPP transactions"),
            new TextLocalizationTranslation(LanguageCodes.German, "OCPP-Transaktionen werden gelöscht"));

        Register(TranslationKeys.ChargingStationDeletionStepConnectorValueLogs,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting connector value logs"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschluss-Datenprotokolle werden gelöscht"));

        Register(TranslationKeys.ChargingStationDeletionStepMeterValues,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting meter values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zählerwerte werden gelöscht"));

        Register(TranslationKeys.ChargingStationDeletionStepConnectorAssignments,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting allowed car assignments"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zugelassene Fahrzeug-Zuordnungen werden gelöscht"));

        Register(TranslationKeys.ChargingStationDeletionStepConnectors,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting connectors"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschlüsse werden gelöscht"));

        Register(TranslationKeys.ChargingStationDeletionStepChargingStation,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleting charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestation wird gelöscht"));

        Register(TranslationKeys.ChargingStationOverviewConnectors,
            new TextLocalizationTranslation(LanguageCodes.English, "Connectors"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschlüsse"));

        Register(TranslationKeys.ChargingStationOverviewPhaseSwitching,
            new TextLocalizationTranslation(LanguageCodes.English, "Phase Switching"),
            new TextLocalizationTranslation(LanguageCodes.German, "Phasenumschaltung"));

        Register(TranslationKeys.ChargingStationOverviewSupported,
            new TextLocalizationTranslation(LanguageCodes.English, "Supported"),
            new TextLocalizationTranslation(LanguageCodes.German, "Unterstützt"));

        Register(TranslationKeys.ChargingStationOverviewNotSupported,
            new TextLocalizationTranslation(LanguageCodes.English, "Not Supported"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht unterstützt"));

        Register(TranslationKeys.ChargingStationEditConnectors,
            new TextLocalizationTranslation(LanguageCodes.English, "Connectors"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschlüsse"));
    }
}
