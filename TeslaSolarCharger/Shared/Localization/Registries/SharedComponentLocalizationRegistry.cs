using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class SharedComponentLocalizationRegistry : TextLocalizationRegistry<SharedComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.GeneralBaseConfiguration,
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));

        Register(TranslationKeys.GeneralSave,
            new TextLocalizationTranslation(LanguageCodes.English, "Save"),
            new TextLocalizationTranslation(LanguageCodes.German, "Speichern"));

        Register(TranslationKeys.GeneralDelete,
            new TextLocalizationTranslation(LanguageCodes.English, "Delete"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen"));

        Register(TranslationKeys.GeneralCancel,
            new TextLocalizationTranslation(LanguageCodes.English, "Cancel"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abbrechen"));

        Register(TranslationKeys.GeneralProcessing,
            new TextLocalizationTranslation(LanguageCodes.English, "Processing"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird verarbeitet"));

        Register(TranslationKeys.GeneralSaved,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert."));

        Register(TranslationKeys.GeneralLoading,
            new TextLocalizationTranslation(LanguageCodes.English, "Loading..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird geladen..."));

        Register(TranslationKeys.GeneralCarSettings,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

        Register(TranslationKeys.GeneralChargingStations,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.GeneralConnectedViaOcpp,
            new TextLocalizationTranslation(LanguageCodes.English, "connected via OCPP"),
            new TextLocalizationTranslation(LanguageCodes.German, "über OCPP verbunden"));

        Register(TranslationKeys.GeneralPluggedIn,
            new TextLocalizationTranslation(LanguageCodes.English, "plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "eingesteckt"));

        Register(TranslationKeys.GeneralCharging,
            new TextLocalizationTranslation(LanguageCodes.English, "charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "lädt"));

        Register(TranslationKeys.UnitWatts,
            new TextLocalizationTranslation(LanguageCodes.English, "W"),
            new TextLocalizationTranslation(LanguageCodes.German, "W"));

        Register(TranslationKeys.UnitAmpere,
            new TextLocalizationTranslation(LanguageCodes.English, "A"),
            new TextLocalizationTranslation(LanguageCodes.German, "A"));

        Register(TranslationKeys.UnitPercent,
            new TextLocalizationTranslation(LanguageCodes.English, "%"),
            new TextLocalizationTranslation(LanguageCodes.German, "%"));

        Register(TranslationKeys.UnitWh,
            new TextLocalizationTranslation(LanguageCodes.English, "Wh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wh"));

        Register(TranslationKeys.UnitMB,
            new TextLocalizationTranslation(LanguageCodes.English, "MB"),
            new TextLocalizationTranslation(LanguageCodes.German, "MB"));

        Register(TranslationKeys.UnitSeconds,
            new TextLocalizationTranslation(LanguageCodes.English, "s"),
            new TextLocalizationTranslation(LanguageCodes.German, "s"));

        Register(TranslationKeys.MainLayoutAbout,
            new TextLocalizationTranslation(LanguageCodes.English, "About"),
            new TextLocalizationTranslation(LanguageCodes.German, "Über"));

        Register(TranslationKeys.MainLayoutUnhandledError,
            new TextLocalizationTranslation(LanguageCodes.English, "An unhandled error has occurred."),
            new TextLocalizationTranslation(LanguageCodes.German, "Es ist ein Fehler aufgetreten."));

        Register(TranslationKeys.MainLayoutReload,
            new TextLocalizationTranslation(LanguageCodes.English, "Reload"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neu laden"));

        Register(TranslationKeys.GenericInputMultiSelectionText,
            new TextLocalizationTranslation(LanguageCodes.English, "{0}{1} item{2} been selected"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0}{1} Element{2} ausgewählt"));

        Register(TranslationKeys.TeslaPowerwallEditFormError,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not load energy sites. Check "),
            new TextLocalizationTranslation(LanguageCodes.German, "Konnte Energiestandorte nicht laden. Prüfen Sie "));

        Register(TranslationKeys.TeslaPowerwallEditFormCloudConnectionLink,
            new TextLocalizationTranslation(LanguageCodes.English, "Cloud Connection"),
            new TextLocalizationTranslation(LanguageCodes.German, "Cloud-Verbindung"));

        Register(TranslationKeys.TeslaPowerwallEditFormErrorSuffix,
            new TextLocalizationTranslation(LanguageCodes.English, " if everything is all right with your Tesla Fleet API connection."),
            new TextLocalizationTranslation(LanguageCodes.German, ", ob mit Ihrer Tesla Fleet API-Verbindung alles in Ordnung ist."));

        Register(TranslationKeys.RestValueResultConfigurationResultsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Results"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ergebnisse"));

        Register(TranslationKeys.RestPvValueUseModbusUrlCreationTool,
            new TextLocalizationTranslation(LanguageCodes.English, "Use Modbus Url Creation Tool"),
            new TextLocalizationTranslation(LanguageCodes.German, "Modbus-URL-Erstellungstool verwenden"));

        Register(TranslationKeys.RestPvValueModbusUrlCreationToolHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Use this to configure URL for Modbus plugin."),
            new TextLocalizationTranslation(LanguageCodes.German, "Verwenden Sie dies, um die URL für das Modbus-Plugin zu konfigurieren."));

        Register(TranslationKeys.RestPvValueHeadersTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Headers"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kopfzeilen"));

        Register(TranslationKeys.RestPvValueAddNewHeaderButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add new header"),
            new TextLocalizationTranslation(LanguageCodes.German, "Neue Kopfzeile hinzufügen"));
    }
}
