using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ManualOcppChargingComponentLocalizationRegistry : TextLocalizationRegistry<ManualOcppChargingComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ManualOcppCurrentToSetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Current to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke festlegen"));

        Register(TranslationKeys.ManualOcppPhasesLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Phasen"));

        Register(TranslationKeys.ManualOcppPhaseSwitchingDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "If your Charging station supports phase switching, you can set the number of phases here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wenn deine Ladestation das Umschalten der Phasen unterstützt, kannst du hier die Anzahl der Phasen festlegen."));

        Register(TranslationKeys.ManualOcppStartChargingButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Start Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden starten"));

        Register(TranslationKeys.ManualOcppStopChargingButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Stop Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden stoppen"));

        Register(TranslationKeys.ManualOcppSetCurrentAndPhasesButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Current and Phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke und Phasen setzen"));

        Register(TranslationKeys.ManualOcppConnectorIdMissingError,
            new TextLocalizationTranslation(LanguageCodes.English, "ChargingConnectorId not set"),
            new TextLocalizationTranslation(LanguageCodes.German, "ChargingConnectorId nicht gesetzt"));

        Register(TranslationKeys.ManualOcppCurrentRequiredMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Current required"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke erforderlich"));

        Register(TranslationKeys.ManualOcppCurrentRequiredWithPeriodMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Current required."),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke erforderlich."));

        Register(TranslationKeys.ManualOcppErrorFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Error: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler: {0}"));

        Register(TranslationKeys.ManualOcppCommandSuccessMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Command successfully sent"),
            new TextLocalizationTranslation(LanguageCodes.German, "Befehl erfolgreich gesendet"));
    }
}
