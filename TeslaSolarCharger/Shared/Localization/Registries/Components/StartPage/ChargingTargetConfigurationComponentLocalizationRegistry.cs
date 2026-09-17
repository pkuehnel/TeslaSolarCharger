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
            new TextLocalizationTranslation(LanguageCodes.English, "This element was saved in a different timezone than your device currently is in. The timezone is set when adding a new target, so to fix this issue, you need to delete this target and re-add it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Element wurde in einer anderen Zeitzone gespeichert, als sich Ihr Gerät derzeit befindet. Die Zeitzone wird beim Hinzufügen eines neuen Ziels festgelegt. Um dieses Problem zu beheben, müssen Sie dieses Ziel löschen und erneut hinzufügen."));

        Register(TranslationKeys.ChargingTargetsGridPricesUnknown,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid prices unknown"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreise unbekannt"));

        Register(TranslationKeys.ChargingTargetsGridPricesUnknownContent,
            new TextLocalizationTranslation(LanguageCodes.English, "The target time is further in the future than the grid prices are known. No charging schedules will be created for this target until grid prices are known."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Zielzeit liegt weiter in der Zukunft, als die Netzpreise bekannt sind. Für dieses Ziel werden keine Ladepläne erstellt, bis die Netzpreise bekannt sind."));

        Register(TranslationKeys.ChargingTargetsDeleted,
            new TextLocalizationTranslation(LanguageCodes.English, "Deleted."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gelöscht."));

        Register(TranslationKeys.ChargingTargetsTargetSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Target SoC: {0}%"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ziel-SoC: {0}%"));

        Register(TranslationKeys.ChargingTargetsDischargeHomeBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Discharge home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimbatterie entladen"));

        Register(TranslationKeys.ChargingTargetsTargetTime,
            new TextLocalizationTranslation(LanguageCodes.English, "Target time: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zielzeit: {0}"));

        Register(TranslationKeys.ChargingTargetsNoTimeConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "No target time configured"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Zielzeit konfiguriert"));

        Register(TranslationKeys.ChargingTargetsRepeatsOn,
            new TextLocalizationTranslation(LanguageCodes.English, "Repeats on {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wiederholt sich am {0}"));

        Register(TranslationKeys.ChargingTargetsRunsOn,
            new TextLocalizationTranslation(LanguageCodes.English, "Runs on {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Läuft am {0}"));

        Register(TranslationKeys.ChargingTargetsNoDateConfigured,
            new TextLocalizationTranslation(LanguageCodes.English, "No date configured"),
            new TextLocalizationTranslation(LanguageCodes.German, "Kein Datum konfiguriert"));

        Register(TranslationKeys.ChargingTargetsHomeBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimbatterie"));

        Register(TranslationKeys.ChargingTargetsDischargeToMinSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Will be discharged to its minimum state of charge by the target time"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird bis zur Zielzeit auf ihren Mindest-Ladestand entladen"));

        Register(TranslationKeys.ChargingTargetsReduceChargingSpeed,
            new TextLocalizationTranslation(LanguageCodes.English, "The car's charging speed is reduced to the home battery's maximum discharge power, so no grid energy is used as long as the home battery has enough energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Ladegeschwindigkeit des Fahrzeugs wird auf die maximale Entladeleistung der Heimbatterie reduziert, sodass keine Netzenergie verwendet wird, solange die Heimbatterie über ausreichend Energie verfügt"));

        Register(TranslationKeys.ChargingTargetsDontReduceChargingSpeed,
            new TextLocalizationTranslation(LanguageCodes.English, "The car's charging speed is not reduced to the home battery's maximum discharge power, so grid energy may be used even if the home battery would have enough energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Die Ladegeschwindigkeit des Fahrzeugs wird nicht auf die maximale Entladeleistung der Heimbatterie reduziert, es kann damit Netzenergie verwendet werden, auch wenn die Heimbatterie einen ausreichenden Ladestand hätte"));

        Register(TranslationKeys.ChargingTargetDialogTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging target"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeziel"));

        Register(TranslationKeys.ChargingTargetTimezoneWarningTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved in different timezone"),
            new TextLocalizationTranslation(LanguageCodes.German, "In anderer Zeitzone gespeichert"));

        Register(TranslationKeys.ChargingTargetTimezoneWarningContent,
            new TextLocalizationTranslation(LanguageCodes.English, "This element was saved in a different timezone than your device currently is in. The timezone is set when adding a new target, so to fix this issue, you need to delete this target and re-add it."),
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

        Register(TranslationKeys.ChargingTargetsDeleteFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Could not delete: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konnte nicht gelöscht werden: {0}"));
    }
}
