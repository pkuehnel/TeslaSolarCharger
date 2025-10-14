using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingConnectorDetailsComponentLocalizationRegistry : TextLocalizationRegistry<ChargingConnectorDetailsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Charging Connector is not set.",
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Connector is not set."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschluss ist nicht festgelegt."));

        Register("Failed to update Charge Mode: {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Charge Mode: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus konnte nicht aktualisiert werden: {0}"));

        Register("Charge Mode updated successfully.",
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Mode updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus erfolgreich aktualisiert."));
    }
}
