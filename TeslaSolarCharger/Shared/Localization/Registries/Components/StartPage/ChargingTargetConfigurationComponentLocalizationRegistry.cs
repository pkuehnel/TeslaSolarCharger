using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingTargetConfigurationComponentLocalizationRegistry : TextLocalizationRegistry<ChargingTargetConfigurationComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Charging Targets",
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Targets"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeziele"));

        Register("Add Target",
            new TextLocalizationTranslation(LanguageCodes.English, "Add Target"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ziel hinzufügen"));

        Register("Nothing planned",
            new TextLocalizationTranslation(LanguageCodes.English, "Nothing planned"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nichts geplant"));

        Register("Saved in different timezone",
            new TextLocalizationTranslation(LanguageCodes.English, "Saved in different timezone"),
            new TextLocalizationTranslation(LanguageCodes.German, "In anderer Zeitzone gespeichert"));

        Register("This element was saved in a different timezone than your device currently is in. The timezone is set when adding a new target, so to fix this issue, you need to delete this target and readd it.",
            new TextLocalizationTranslation(LanguageCodes.English, "This element was saved in a different timezone than your device currently is in. The timezone is set when adding a new target, so to fix this issue, you need to delete this target and readd it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Element wurde in einer anderen Zeitzone gespeichert als dein Gerät sich aktuell befindet. Die Zeitzone wird beim Hinzufügen eines neuen Ziels festgelegt. Um dieses Problem zu beheben, lösche das Ziel und füge es erneut hinzu."));

        Register("Could not delete: {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Could not delete: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen fehlgeschlagen: {0}"));

        Register("Deleted.",
            new TextLocalizationTranslation(LanguageCodes.English, "Deleted."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gelöscht."));

        Register("Charging target",
            new TextLocalizationTranslation(LanguageCodes.English, "Charging target"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeziel"));

        Register("Target SoC: {0}%",
            new TextLocalizationTranslation(LanguageCodes.English, "Target SoC: {0}%"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ziel-SoC: {0}%"));

        Register("Discharge home battery",
            new TextLocalizationTranslation(LanguageCodes.English, "Discharge home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeicher entladen"));

        Register("Target time: {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Target time: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zielzeit: {0}"));

        Register("No target time configured",
            new TextLocalizationTranslation(LanguageCodes.English, "No target time configured"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Zielzeit konfiguriert"));

        Register("Repeats on {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Repeats on {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederholt sich am {0}"));

        Register("Runs on {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Runs on {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Läuft am {0}"));

        Register("No date configured",
            new TextLocalizationTranslation(LanguageCodes.English, "No date configured"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kein Datum konfiguriert"));

        Register("Home battery",
            new TextLocalizationTranslation(LanguageCodes.English, "Home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeicher"));

        Register("Discharge to min SoC",
            new TextLocalizationTranslation(LanguageCodes.English, "Discharge to min SoC"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bis zum minimalen SoC entladen"));

        Register("Try to not use grid energy by reducing car's charging speed",
            new TextLocalizationTranslation(LanguageCodes.English, "Try to not use grid energy by reducing car's charging speed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Versuche Netzstrom zu vermeiden, indem die Ladegeschwindigkeit des Autos reduziert wird"));

        Register("Don't reduce cars's charging speed, grid energy may be used even if home battery has enough energy",
            new TextLocalizationTranslation(LanguageCodes.English, "Don't reduce cars's charging speed, grid energy may be used even if home battery has enough energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladegeschwindigkeit des Autos nicht reduzieren; Netzstrom kann verwendet werden, auch wenn der Heimspeicher genügend Energie hat"));
    }
}
