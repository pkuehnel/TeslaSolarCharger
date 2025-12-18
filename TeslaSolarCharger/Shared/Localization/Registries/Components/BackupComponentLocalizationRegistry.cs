using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class BackupComponentLocalizationRegistry : TextLocalizationRegistry<BackupComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.BackupRestorePageTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Backup and Restore"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sichern und Wiederherstellen"));

        Register(TranslationKeys.BackupRestoreInfoText,
            new TextLocalizationTranslation(LanguageCodes.English, "During the backup or restore process all TSC actions will be stopped and started again after the Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Während des Sicherungs- oder Wiederherstellungsvorgangs werden alle TSC-Aktionen gestoppt und nach der Sicherung wieder gestartet"));

        Register(TranslationKeys.BackupSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sicherung"));

        Register(TranslationKeys.BackupWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: The backup contains private information like password for your database, possibly access codes to your solar system, latest known location of your car(s),... Do not share the file in public."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Die Sicherung enthält private Informationen wie das Passwort für Ihre Datenbank, möglicherweise Zugangscodes zu Ihrer Solaranlage, den letzten bekannten Standort Ihres Autos usw. Geben Sie die Datei nicht öffentlich weiter."));

        Register(TranslationKeys.BackupProcessingMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Backup creation might take a few minutes, please wait..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Sicherungserstellung kann einige Minuten dauern, bitte warten..."));

        Register(TranslationKeys.BackupStartButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Start Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sicherung starten"));

        Register(TranslationKeys.RestoreSectionTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Restore"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederherstellen"));

        Register(TranslationKeys.RestoreWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "After the restore process you need to restart the TSC container."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nach dem Wiederherstellungsvorgang müssen Sie den TSC-Container neu starten."));

        Register(TranslationKeys.RestoreSelectFileButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Select Backup File"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sicherungsdatei auswählen"));

        Register(TranslationKeys.RestoreStartButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Start restore"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederherstellung starten"));

        Register(TranslationKeys.RestoreAutoBackupsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Automatically created backups before each update"),
            new TextLocalizationTranslation(LanguageCodes.German, "Automatisch erstellte Sicherungen vor jedem Update"));

        Register(TranslationKeys.RestoreDownloadButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Download"),
            new TextLocalizationTranslation(LanguageCodes.German, "Herunterladen"));

        Register(TranslationKeys.RestoreFileTooBigError,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} is greater than {1} and won't be uploaded."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} ist größer als {1} und wird nicht hochgeladen."));

        Register(TranslationKeys.RestoreNoFileSelectedError,
            new TextLocalizationTranslation(LanguageCodes.English, "No file selected"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Datei ausgewählt"));

        Register(TranslationKeys.RestoreError,
            new TextLocalizationTranslation(LanguageCodes.English, "Error while restoring backup: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler beim Wiederherstellen der Sicherung: {0}"));

        Register(TranslationKeys.RestoreSuccessMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Backup file saved. Container restart required to complete restore. Please restart the TSC container now."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sicherungsdatei gespeichert. Neustart des Containers erforderlich, um die Wiederherstellung abzuschließen. Bitte starten Sie den TSC-Container jetzt neu."));

        Register(TranslationKeys.RestoreFatalError,
            new TextLocalizationTranslation(LanguageCodes.English, "Fatal Error while restoring backup: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schwerwiegender Fehler beim Wiederherstellen der Sicherung: {0}"));

        Register(TranslationKeys.RestoreRefreshError,
            new TextLocalizationTranslation(LanguageCodes.English, "Error while refreshing backups: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler beim Aktualisieren der Sicherungen: {0}"));

        Register(TranslationKeys.RestoreNoBackupsFound,
            new TextLocalizationTranslation(LanguageCodes.English, "No backups found"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Sicherungen gefunden"));
    }
}
