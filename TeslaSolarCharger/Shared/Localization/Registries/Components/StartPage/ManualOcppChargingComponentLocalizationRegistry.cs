using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ManualOcppChargingComponentLocalizationRegistry : TextLocalizationRegistry<ManualOcppChargingComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ManualOcppCurrentToSet,
            new TextLocalizationTranslation(LanguageCodes.English, "Current to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einzustellender Strom"));

        Register(TranslationKeys.ManualOcppPhases,
            new TextLocalizationTranslation(LanguageCodes.English, "Phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Phasen"));

        Register(TranslationKeys.ManualOcppPhasesHint,
            new TextLocalizationTranslation(LanguageCodes.English, "If your Charging station supports phase switching, you can set the number of phases here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wenn Ihre Ladestation Phasenumschaltung unterstützt, können Sie hier die Anzahl der Phasen einstellen."));

        Register(TranslationKeys.ManualOcppStart,
            new TextLocalizationTranslation(LanguageCodes.English, "Start Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden starten"));

        Register(TranslationKeys.ManualOcppStop,
            new TextLocalizationTranslation(LanguageCodes.English, "Stop Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden stoppen"));

        Register(TranslationKeys.ManualOcppSetCurrentAndPhases,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Current and Phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strom und Phasen setzen"));

        Register(TranslationKeys.ManualOcppConnectorNotSetError,
            new TextLocalizationTranslation(LanguageCodes.English, "ChargingConnectorId not set"),
            new TextLocalizationTranslation(LanguageCodes.German, "ChargingConnectorId nicht gesetzt"));

        Register(TranslationKeys.ManualOcppCurrentRequiredError,
            new TextLocalizationTranslation(LanguageCodes.English, "Current required"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strom erforderlich"));

        Register(TranslationKeys.ManualOcppCommandSent,
            new TextLocalizationTranslation(LanguageCodes.English, "Command successfully sent"),
            new TextLocalizationTranslation(LanguageCodes.German, "Befehl erfolgreich gesendet"));

        Register(TranslationKeys.ManualOcppErrorFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Error: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler: {0}"));
    }
}
