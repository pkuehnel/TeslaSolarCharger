using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class SharedComponentLocalizationRegistry : TextLocalizationRegistry<SharedComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.GeneralBaseConfiguration,
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));

        Register(TranslationKeys.GeneralSave,
            new TextLocalizationTranslation(LanguageCodes.English, "Save"),
            new TextLocalizationTranslation(LanguageCodes.German, "Speichern"));

        Register(TranslationKeys.GeneralCancel,
            new TextLocalizationTranslation(LanguageCodes.English, "Cancel"),
            new TextLocalizationTranslation(LanguageCodes.German, "Abbrechen"));

        Register(TranslationKeys.GeneralProcessing,
            new TextLocalizationTranslation(LanguageCodes.English, "Processing"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird verarbeitet"));

        Register(TranslationKeys.GeneralSaved,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert."));

        Register(TranslationKeys.GeneralLoading,
            new TextLocalizationTranslation(LanguageCodes.English, "Loading..."),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird geladen..."));

        Register(TranslationKeys.GeneralCarSettings,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Settings"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugeinstellungen"));

        Register(TranslationKeys.GeneralChargingStations,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.GeneralConnectedViaOcpp,
            new TextLocalizationTranslation(LanguageCodes.English, "connected via OCPP"),
            new TextLocalizationTranslation(LanguageCodes.German, "über OCPP verbunden"));

        Register(TranslationKeys.GeneralPluggedIn,
            new TextLocalizationTranslation(LanguageCodes.English, "plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "eingesteckt"));

        Register(TranslationKeys.GeneralCharging,
            new TextLocalizationTranslation(LanguageCodes.English, "charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "lädt"));

        Register(TranslationKeys.UnitWatts,
            new TextLocalizationTranslation(LanguageCodes.English, "W"),
            new TextLocalizationTranslation(LanguageCodes.German, "W"));

        Register(TranslationKeys.UnitAmpere,
            new TextLocalizationTranslation(LanguageCodes.English, "A"),
            new TextLocalizationTranslation(LanguageCodes.German, "A"));

        Register(TranslationKeys.UnitPercent,
            new TextLocalizationTranslation(LanguageCodes.English, "%"),
            new TextLocalizationTranslation(LanguageCodes.German, "%"));

        Register(TranslationKeys.UnitWh,
            new TextLocalizationTranslation(LanguageCodes.English, "Wh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wh"));
    }
}
