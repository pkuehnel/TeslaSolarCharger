using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class BackupComponentLocalizationRegistry : TextLocalizationRegistry<BackupComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Backup and Restore",
            new TextLocalizationTranslation(LanguageCodes.English, "Backup and Restore"),
            new TextLocalizationTranslation(LanguageCodes.German, "Backup und Wiederherstellung"));

        Register("During the backup or restore process all TSC actions will be stopped and started again after the Backup",
            new TextLocalizationTranslation(LanguageCodes.English, "During the backup or restore process all TSC actions will be stopped and started again after the Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Während des Backup- oder Wiederherstellungsprozesses werden alle TSC-Aktionen gestoppt und nach dem Backup erneut gestartet."));

        Register("Backup",
            new TextLocalizationTranslation(LanguageCodes.English, "Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Backup"));

        Register("Note: The backup contains private information like password for your database, possibly access codes to your solar system, latest known location of your car(s),... Do not share the backup in public.",
            new TextLocalizationTranslation(LanguageCodes.English, "Note: The backup contains private information like password for your database, possibly access codes to your solar system, latest known location of your car(s),... Do not share the backup in public."),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Das Backup enthält vertrauliche Informationen wie das Passwort für Ihre Datenbank, möglicherweise Zugangsdaten zu Ihrer Solaranlage, den zuletzt bekannten Standort Ihres/Ihrer Fahrzeugs/Fahrzeuge usw. Geben Sie das Backup nicht öffentlich weiter."));

        Register("Backup creation might take a few minutes, please wait...",
            new TextLocalizationTranslation(LanguageCodes.English, "Backup creation might take a few minutes, please wait..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Erstellung des Backups kann einige Minuten dauern, bitte warten..."));

        Register("Start Backup",
            new TextLocalizationTranslation(LanguageCodes.English, "Start Backup"),
            new TextLocalizationTranslation(LanguageCodes.German, "Backup starten"));

        Register("Restore",
            new TextLocalizationTranslation(LanguageCodes.English, "Restore"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederherstellen"));

        Register("After the restore process you need to restart the TSC container.",
            new TextLocalizationTranslation(LanguageCodes.English, "After the restore process you need to restart the TSC container."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nach dem Wiederherstellungsprozess müssen Sie den TSC-Container neu starten."));

        Register("Select Backup File",
            new TextLocalizationTranslation(LanguageCodes.English, "Select Backup File"),
            new TextLocalizationTranslation(LanguageCodes.German, "Backup-Datei auswählen"));

        Register("Start restore",
            new TextLocalizationTranslation(LanguageCodes.English, "Start restore"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederherstellung starten"));

        Register("Automatically created backups before each update",
            new TextLocalizationTranslation(LanguageCodes.English, "Automatically created backups before each update"),
            new TextLocalizationTranslation(LanguageCodes.German, "Automatisch erstellte Backups vor jedem Update"));

        Register("Download",
            new TextLocalizationTranslation(LanguageCodes.English, "Download"),
            new TextLocalizationTranslation(LanguageCodes.German, "Herunterladen"));

        Register("{0} is greater than {1} and won't be uploaded.",
            new TextLocalizationTranslation(LanguageCodes.English, "{0} is greater than {1} and won't be uploaded."),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} ist größer als {1} und wird nicht hochgeladen."));

        Register("No file selected",
            new TextLocalizationTranslation(LanguageCodes.English, "No file selected"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Datei ausgewählt"));

        Register("Error while restoring backup: {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Error while restoring backup: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler bei der Wiederherstellung des Backups: {0}"));

        Register("Backup file saved. Container restart required to complete restore. Please restart the TSC container now.",
            new TextLocalizationTranslation(LanguageCodes.English, "Backup file saved. Container restart required to complete restore. Please restart the TSC container now."),
            new TextLocalizationTranslation(LanguageCodes.German, "Backup-Datei gespeichert. Zum Abschluss der Wiederherstellung ist ein Neustart des Containers erforderlich. Bitte starten Sie den TSC-Container jetzt neu."));

        Register("Fatal Error while restoring backup: {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Fatal Error while restoring backup: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Schwerer Fehler bei der Wiederherstellung des Backups: {0}"));

        Register("No backups found",
            new TextLocalizationTranslation(LanguageCodes.English, "No backups found"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Backups gefunden"));

        Register("Error while refreshing backups: {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Error while refreshing backups: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler beim Aktualisieren der Backups: {0}"));
    }
}
