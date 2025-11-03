using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingTargetConfigurationComponentLocalizationRegistry : TextLocalizationRegistry<ChargingTargetConfigurationComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingTargets,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Targets"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeziele"));

        Register(TranslationKeys.AddTarget,
            new TextLocalizationTranslation(LanguageCodes.English, "Add Target"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ziel hinzufügen"));

        Register(TranslationKeys.NothingPlanned,
            new TextLocalizationTranslation(LanguageCodes.English, "Nothing planned"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nichts geplant"));

        Register(TranslationKeys.SavedInDifferentTimezone,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved in different timezone"),
            new TextLocalizationTranslation(LanguageCodes.German, "In anderer Zeitzone gespeichert"));

        Register(TranslationKeys.ThisElementWasSavedInA,
            new TextLocalizationTranslation(LanguageCodes.English, "This element was saved in a different timezone than your device currently is in. The timezone is set when adding a new target, so to fix this issue, you need to delete this target and readd it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Element wurde in einer anderen Zeitzone gespeichert als dein Gerät sich aktuell befindet. Die Zeitzone wird beim Hinzufügen eines neuen Ziels festgelegt. Um dieses Problem zu beheben, lösche das Ziel und füge es erneut hinzu."));

        Register(TranslationKeys.CouldNotDelete0,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not delete: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Löschen fehlgeschlagen: {0}"));

        Register(TranslationKeys.Deleted,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleted."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gelöscht."));

        Register(TranslationKeys.ChargingTarget,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging target"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeziel"));

        Register(TranslationKeys.TargetSoc0,
            new TextLocalizationTranslation(LanguageCodes.English, "Target SoC: {0}%"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ziel-Ladestand: {0}%"));

        Register(TranslationKeys.RepeatOn,
            new TextLocalizationTranslation(LanguageCodes.English, "Repeat on:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederholen am:"));

        Register(TranslationKeys.DischargeHomeBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Discharge home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeicher entladen"));

        Register(TranslationKeys.TargetTime0,
            new TextLocalizationTranslation(LanguageCodes.English, "Target time: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zielzeit: {0}"));

        Register(TranslationKeys.NoTargetTimeConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "No target time configured"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Zielzeit konfiguriert"));

        Register(TranslationKeys.RepeatsOn0,
            new TextLocalizationTranslation(LanguageCodes.English, "Repeats on {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederholt sich am {0}"));

        Register(TranslationKeys.RunsOn0,
            new TextLocalizationTranslation(LanguageCodes.English, "Runs on {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Läuft am {0}"));

        Register(TranslationKeys.NoDateConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "No date configured"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kein Datum konfiguriert"));

        Register(TranslationKeys.HomeBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeicher"));

        Register(TranslationKeys.DischargeToMinSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Discharge to min SoC"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bis zum minimalen Ladestand entladen"));

        Register(TranslationKeys.TryToNotUseGridEnergy,
            new TextLocalizationTranslation(LanguageCodes.English, "Try to not use grid energy by reducing car's charging speed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Versuche Netzstrom zu vermeiden, indem die Ladegeschwindigkeit des Autos reduziert wird"));

        Register(TranslationKeys.DonTReduceCarsSCharging,
            new TextLocalizationTranslation(LanguageCodes.English, "Don't reduce cars's charging speed, grid energy may be used even if home battery has enough energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladegeschwindigkeit des Autos nicht reduzieren; Netzstrom kann verwendet werden, auch wenn der Heimspeicher genügend Energie hat"));
    }
}
