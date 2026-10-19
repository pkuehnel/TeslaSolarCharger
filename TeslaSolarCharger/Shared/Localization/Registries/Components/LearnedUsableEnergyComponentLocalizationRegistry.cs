namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class LearnedUsableEnergyComponentLocalizationRegistry : TextLocalizationRegistry<LearnedUsableEnergyComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.UsableEnergyLearnedInUse,
            new TextLocalizationTranslation(LanguageCodes.English, "Learned from {0} measurements: {1} kWh. Used instead of the value above."),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus {0} Messungen gelernt: {1} kWh. Wird statt des Werts oben verwendet."));

        Register(TranslationKeys.UsableEnergyLearnedNotUsedLearningOff,
            new TextLocalizationTranslation(LanguageCodes.English, "Learned from past charges: {0} kWh. Not used as learning is switched off in the base configuration."),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus vergangenen Ladungen gelernt: {0} kWh. Wird nicht verwendet, da das Lernen in der Basiskonfiguration ausgeschaltet ist."));

        Register(TranslationKeys.UsableEnergyHomeBatteryLearnedValues,
            new TextLocalizationTranslation(LanguageCodes.English, "charging {0} kWh, discharging {1} kWh ({2} % of the charged energy comes back out)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden {0} kWh, Entladen {1} kWh ({2} % der geladenen Energie kommen wieder heraus)"));

        Register(TranslationKeys.UsableEnergyHomeBatteryLearnedInUse,
            new TextLocalizationTranslation(LanguageCodes.English, "Learned from {0} measurements: {1}. Used instead of the value above."),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus {0} Messungen gelernt: {1}. Wird statt des Werts oben verwendet."));

        Register(TranslationKeys.UsableEnergyHomeBatteryLearnedNotUsedLearningOff,
            new TextLocalizationTranslation(LanguageCodes.English, "Learned from past charges and discharges: {0}. Not used as learning is switched off in the base configuration."),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus vergangenen Lade- und Entladevorgängen gelernt: {0}. Wird nicht verwendet, da das Lernen in der Basiskonfiguration ausgeschaltet ist."));

        Register(TranslationKeys.UsableEnergyDischargingExceedsCharging,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Not used: the home battery seems to deliver more than is charged into it (charging {0} kWh, discharging {1} kWh), which is impossible. Please check the home battery power and SoC values. Until then the value above is used."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Nicht verwendet: die Heimbatterie scheint mehr abzugeben, als in sie geladen wird (Laden {0} kWh, Entladen {1} kWh), was unmöglich ist. Bitte prüfen Sie die Werte für Leistung und Ladestand der Heimbatterie. Bis dahin wird der Wert oben verwendet."));

        Register(TranslationKeys.UsableEnergyLearningOff,
            new TextLocalizationTranslation(LanguageCodes.English, "Learning the usable energy from past charges is switched off in the base configuration."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Lernen der nutzbaren Energie aus vergangenen Ladungen ist in der Basiskonfiguration ausgeschaltet."));

        Register(TranslationKeys.UsableEnergyLearningProgress,
            new TextLocalizationTranslation(LanguageCodes.English, "Learning from past {0}: {1} of {2} suitable ones so far. Until then the value above is used."),
            new TextLocalizationTranslation(LanguageCodes.German, "Lernen aus vergangenen {0}: bisher {1} von {2} geeigneten. Bis dahin wird der Wert oben verwendet."));

        Register(TranslationKeys.UsableEnergyCarNotMeasuredYet,
            new TextLocalizationTranslation(LanguageCodes.English,
                "No charges measured yet. Past charges are measured 5 minutes after Solar4Car starts and every night. Until a value is learned the value above is used."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Noch keine Ladungen gemessen. Vergangene Ladungen werden 5 Minuten nach dem Start von Solar4Car und jede Nacht gemessen. Bis ein Wert gelernt ist, wird der Wert oben verwendet."));

        Register(TranslationKeys.UsableEnergyLearningInconsistent,
            new TextLocalizationTranslation(LanguageCodes.English, "Not used yet: the past {0} vary too much (±{1} %, at most ±{2} % allowed), so the value above is used."),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch nicht verwendet: die vergangenen {0} weichen zu stark voneinander ab (±{1} %, erlaubt sind höchstens ±{2} %), daher wird der Wert oben verwendet."));

        Register(TranslationKeys.UsableEnergyMultipleHomeBatteries,
            new TextLocalizationTranslation(LanguageCodes.English, "The usable energy can not be learned with more than one home battery, so the value above is used."),
            new TextLocalizationTranslation(LanguageCodes.German, "Mit mehr als einer Heimbatterie kann die nutzbare Energie nicht gelernt werden, daher wird der Wert oben verwendet."));

        Register(TranslationKeys.UsableEnergyCarDeviationWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "The learned value differs by {0} % from the entered one. If this surprises you, check the entered value and the car charge loss in the base configuration."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der gelernte Wert weicht um {0} % vom eingegebenen ab. Falls Sie das überrascht, prüfen Sie den eingegebenen Wert und den Ladeverlust des Fahrzeugs in der Basiskonfiguration."));

        Register(TranslationKeys.UsableEnergyHomeBatteryDeviationWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "The learned value differs by {0} % from the entered one. If this surprises you, check the entered value."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der gelernte Wert weicht um {0} % vom eingegebenen ab. Falls Sie das überrascht, prüfen Sie den eingegebenen Wert."));

        Register(TranslationKeys.UsableEnergyKindCarCharge,
            new TextLocalizationTranslation(LanguageCodes.English, "charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladungen"));

        Register(TranslationKeys.UsableEnergyKindHomeBatteryCharge,
            new TextLocalizationTranslation(LanguageCodes.English, "charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladungen"));

        Register(TranslationKeys.UsableEnergyKindHomeBatteryDischarge,
            new TextLocalizationTranslation(LanguageCodes.English, "discharges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Entladungen"));

        Register(TranslationKeys.UsableEnergyShowDetails,
            new TextLocalizationTranslation(LanguageCodes.English, "Show measurements"),
            new TextLocalizationTranslation(LanguageCodes.German, "Messungen anzeigen"));

        Register(TranslationKeys.UsableEnergyHideDetails,
            new TextLocalizationTranslation(LanguageCodes.English, "Hide measurements"),
            new TextLocalizationTranslation(LanguageCodes.German, "Messungen ausblenden"));

        Register(TranslationKeys.UsableEnergyDetailsExplanation,
            new TextLocalizationTranslation(LanguageCodes.English,
                "Each measurement divides the energy that moved by the SoC change it caused. The learned value is the median of the newest {0} accepted measurements and is only used once at least {1} of them lie close together. Measurements are updated once a night."),
            new TextLocalizationTranslation(LanguageCodes.German,
                "Jede Messung teilt die geflossene Energie durch die dadurch verursachte Änderung des Ladestands. Der gelernte Wert ist der Median der neuesten {0} akzeptierten Messungen und wird erst verwendet, wenn mindestens {1} davon nah beieinander liegen. Die Messungen werden einmal pro Nacht aktualisiert."));

        Register(TranslationKeys.UsableEnergyNoSamples,
            new TextLocalizationTranslation(LanguageCodes.English, "No measurements yet."),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch keine Messungen."));

        Register(TranslationKeys.UsableEnergyColumnDate,
            new TextLocalizationTranslation(LanguageCodes.English, "Date"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datum"));

        Register(TranslationKeys.UsableEnergyColumnType,
            new TextLocalizationTranslation(LanguageCodes.English, "Type"),
            new TextLocalizationTranslation(LanguageCodes.German, "Art"));

        Register(TranslationKeys.UsableEnergyColumnSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "SoC"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand"));

        Register(TranslationKeys.UsableEnergyColumnEnergy,
            new TextLocalizationTranslation(LanguageCodes.English, "Metered energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gemessene Energie"));

        Register(TranslationKeys.UsableEnergyColumnImplied,
            new TextLocalizationTranslation(LanguageCodes.English, "Usable energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nutzbare Energie"));

        Register(TranslationKeys.UsableEnergyColumnStatus,
            new TextLocalizationTranslation(LanguageCodes.English, "Status"),
            new TextLocalizationTranslation(LanguageCodes.German, "Status"));

        Register(TranslationKeys.UsableEnergyStatusUsed,
            new TextLocalizationTranslation(LanguageCodes.English, "Used"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verwendet"));

        Register(TranslationKeys.UsableEnergyStatusOlder,
            new TextLocalizationTranslation(LanguageCodes.English, "Not used, newer measurements exist"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht verwendet, es gibt neuere Messungen"));

        Register(TranslationKeys.UsableEnergyRejectedMissingSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Ignored: not enough SoC readings during the charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ignoriert: zu wenige Ladestandswerte während der Ladung"));

        Register(TranslationKeys.UsableEnergyRejectedSocDifferenceTooSmall,
            new TextLocalizationTranslation(LanguageCodes.English, "Ignored: SoC changed by less than 20 %"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ignoriert: Ladestand hat sich um weniger als 20 % geändert"));

        Register(TranslationKeys.UsableEnergyRejectedMissingMeterValues,
            new TextLocalizationTranslation(LanguageCodes.English, "Ignored: no energy values"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ignoriert: keine Energiewerte"));

        Register(TranslationKeys.UsableEnergyRejectedMeterValueGap,
            new TextLocalizationTranslation(LanguageCodes.English, "Ignored: energy values are missing for a while"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ignoriert: Energiewerte fehlen eine Zeit lang"));

        Register(TranslationKeys.UsableEnergyRejectedSocJump,
            new TextLocalizationTranslation(LanguageCodes.English, "Ignored: SoC jumped, e.g. a recalibration of the battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ignoriert: Ladestand ist gesprungen, z. B. durch eine Kalibrierung der Batterie"));

        Register(TranslationKeys.UsableEnergyRejectedOppositeEnergyFlow,
            new TextLocalizationTranslation(LanguageCodes.English, "Ignored: the battery was charged and discharged at the same time"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ignoriert: die Batterie wurde gleichzeitig ge- und entladen"));

        Register(TranslationKeys.UsableEnergyRejectedImplausibleCapacity,
            new TextLocalizationTranslation(LanguageCodes.English, "Ignored: no realistic battery size"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ignoriert: keine realistische Batteriegröße"));

        Register(TranslationKeys.UsableEnergyResetButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Forget measurements and learn again"),
            new TextLocalizationTranslation(LanguageCodes.German, "Messungen verwerfen und neu lernen"));

        Register(TranslationKeys.UsableEnergyResetDone,
            new TextLocalizationTranslation(LanguageCodes.English, "Measurements forgotten. Learning starts again with the next charge."),
            new TextLocalizationTranslation(LanguageCodes.German, "Messungen verworfen. Das Lernen beginnt mit der nächsten Ladung neu."));
    }
}
