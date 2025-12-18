using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingTargetConfigurationComponentLocalizationRegistry : TextLocalizationRegistry<ChargingTargetConfigurationComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingTargetsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Targets"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeziele"));

        Register(TranslationKeys.ChargingTargetsAddButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Add Target"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ziel hinzufügen"));

        Register(TranslationKeys.ChargingTargetsNothingPlanned,
            new TextLocalizationTranslation(LanguageCodes.English, "Nothing planned"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nichts geplant"));

        Register(TranslationKeys.ChargingTargetsSavedInDifferentTimezone,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved in different timezone"),
            new TextLocalizationTranslation(LanguageCodes.German, "In anderer Zeitzone gespeichert"));

        Register(TranslationKeys.ChargingTargetsTimezoneMismatchContent,
            new TextLocalizationTranslation(LanguageCodes.English, "This element was saved in a different timezone than your device currently is in. The timezone is set when adding a new target, so to fix this issue, you need to delete this target and readd it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Element wurde in einer anderen Zeitzone gespeichert, als sich Ihr Gerät derzeit befindet. Die Zeitzone wird beim Hinzufügen eines neuen Ziels festgelegt. Um dieses Problem zu beheben, müssen Sie dieses Ziel löschen und erneut hinzufügen."));

        Register(TranslationKeys.ChargingTargetsGridPricesUnknown,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid prices unknown"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strompreise unbekannt"));

        Register(TranslationKeys.ChargingTargetsGridPricesUnknownContent,
            new TextLocalizationTranslation(LanguageCodes.English, "The target time is further in the future than the grid prices are known. No charging schedules will be created for this target until grid prices are known."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Zielzeit liegt weiter in der Zukunft, als die Strompreise bekannt sind. Für dieses Ziel werden keine Ladepläne erstellt, bis die Strompreise bekannt sind."));

        Register(TranslationKeys.ChargingTargetsDeleted,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleted."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gelöscht."));

        Register(TranslationKeys.ChargingTargetsTargetSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Target SoC: {0}%"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ziel-SoC: {0}%"));

        Register(TranslationKeys.ChargingTargetsDischargeHomeBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Discharge home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausbatterie entladen"));

        Register(TranslationKeys.ChargingTargetsTargetTime,
            new TextLocalizationTranslation(LanguageCodes.English, "Target time: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zielzeit: {0}"));

        Register(TranslationKeys.ChargingTargetsNoTimeConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "No target time configured"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Zielzeit konfiguriert"));

        Register(TranslationKeys.ChargingTargetsRepeatsOn,
            new TextLocalizationTranslation(LanguageCodes.English, "Repeats on {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederholt am {0}"));

        Register(TranslationKeys.ChargingTargetsRunsOn,
            new TextLocalizationTranslation(LanguageCodes.English, "Runs on {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Läuft am {0}"));

        Register(TranslationKeys.ChargingTargetsNoDateConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "No date configured"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kein Datum konfiguriert"));

        Register(TranslationKeys.ChargingTargetsHomeBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausbatterie"));

        Register(TranslationKeys.ChargingTargetsDischargeToMinSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Discharge to min SoC"),
            new TextLocalizationTranslation(LanguageCodes.German, "Entladen auf min. SoC"));

        Register(TranslationKeys.ChargingTargetsReduceChargingSpeed,
            new TextLocalizationTranslation(LanguageCodes.English, "Try to not use grid energy by reducing car's charging speed"),
            new TextLocalizationTranslation(LanguageCodes.German, "Versuchen Sie, keine Netzenergie zu verbrauchen, indem Sie die Ladegeschwindigkeit des Autos reduzieren"));

        Register(TranslationKeys.ChargingTargetsDontReduceChargingSpeed,
            new TextLocalizationTranslation(LanguageCodes.English, "Don't reduce cars's charging speed, grid energy may be used even if home battery has enough energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Reduzieren Sie nicht die Ladegeschwindigkeit des Autos, Netzenergie kann verwendet werden, auch wenn die Hausbatterie genügend Energie hat"));

        Register(TranslationKeys.ChargingTargetDialogTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging target"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeziel"));

        Register(TranslationKeys.ChargingTargetTimezoneWarningTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved in different timezone"),
            new TextLocalizationTranslation(LanguageCodes.German, "In anderer Zeitzone gespeichert"));

        Register(TranslationKeys.ChargingTargetTimezoneWarningContent,
            new TextLocalizationTranslation(LanguageCodes.English, "This element was saved in a different timezone than your device currently is in. The timezone is set when adding a new target, so to fix this issue, you need to delete this target and readd it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Element wurde in einer anderen Zeitzone gespeichert, als sich Ihr Gerät derzeit befindet. Die Zeitzone wird beim Hinzufügen eines neuen Ziels festgelegt. Um dieses Problem zu beheben, müssen Sie dieses Ziel löschen und erneut hinzufügen."));

        Register(TranslationKeys.ChargingTargetRepeatOn,
            new TextLocalizationTranslation(LanguageCodes.English, "Repeat on:"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederholen am:"));

        Register(TranslationKeys.ChargingTargetProcessing,
            new TextLocalizationTranslation(LanguageCodes.English, "Processing"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird verarbeitet"));

        Register(TranslationKeys.ChargingTargetSave,
            new TextLocalizationTranslation(LanguageCodes.English, "Save"),
            new TextLocalizationTranslation(LanguageCodes.German, "Speichern"));

        Register(TranslationKeys.ChargingTargetCancel,
            new TextLocalizationTranslation(LanguageCodes.English, "Cancel"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abbrechen"));

        Register(TranslationKeys.ChargingTargetSaved,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert."));
    }
}
