using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingConnectorDetailsComponentLocalizationRegistry : TextLocalizationRegistry<ChargingConnectorDetailsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingConnectorDetailsConnectedViaOcpp,
            new TextLocalizationTranslation(LanguageCodes.English, "connected via OCPP"),
            new TextLocalizationTranslation(LanguageCodes.German, "via OCPP verbunden"));

        Register(TranslationKeys.ChargingConnectorDetailsPluggedIn,
            new TextLocalizationTranslation(LanguageCodes.English, "plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "eingesteckt"));

        Register(TranslationKeys.ChargingConnectorDetailsCharging,
            new TextLocalizationTranslation(LanguageCodes.English, "charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "lädt"));

        Register(TranslationKeys.ChargingConnectorDetailsNotSet,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Connector is not set."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschluss ist nicht festgelegt."));

        Register(TranslationKeys.ChargingConnectorDetailsFailedToUpdateChargeMode,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Charge Mode: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus konnte nicht aktualisiert werden: {0}"));

        Register(TranslationKeys.ChargingConnectorDetailsChargeModeUpdated,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Mode updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus erfolgreich aktualisiert."));
    }
}
