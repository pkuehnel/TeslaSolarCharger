using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ManualOcppChargingComponentLocalizationRegistry : TextLocalizationRegistry<ManualOcppChargingComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Current to set",
            new TextLocalizationTranslation(LanguageCodes.English, "Current to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke festlegen"));

        Register("Phases",
            new TextLocalizationTranslation(LanguageCodes.English, "Phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Phasen"));

        Register("If your Charging station supports phase switching, you can set the number of phases here.",
            new TextLocalizationTranslation(LanguageCodes.English, "If your Charging station supports phase switching, you can set the number of phases here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wenn deine Ladestation das Umschalten der Phasen unterstützt, kannst du hier die Anzahl der Phasen festlegen."));

        Register("Start Charging",
            new TextLocalizationTranslation(LanguageCodes.English, "Start Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden starten"));

        Register("Stop Charging",
            new TextLocalizationTranslation(LanguageCodes.English, "Stop Charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden stoppen"));

        Register("Set Current and Phases",
            new TextLocalizationTranslation(LanguageCodes.English, "Set Current and Phases"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke und Phasen setzen"));

        Register("ChargingConnectorId not set",
            new TextLocalizationTranslation(LanguageCodes.English, "ChargingConnectorId not set"),
            new TextLocalizationTranslation(LanguageCodes.German, "ChargingConnectorId nicht gesetzt"));

        Register("Current required",
            new TextLocalizationTranslation(LanguageCodes.English, "Current required"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke erforderlich"));

        Register("Current required.",
            new TextLocalizationTranslation(LanguageCodes.English, "Current required."),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke erforderlich."));

        Register("Error: {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Error: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler: {0}"));

        Register("Command successfully sent",
            new TextLocalizationTranslation(LanguageCodes.English, "Command successfully sent"),
            new TextLocalizationTranslation(LanguageCodes.German, "Befehl erfolgreich gesendet"));
    }
}
