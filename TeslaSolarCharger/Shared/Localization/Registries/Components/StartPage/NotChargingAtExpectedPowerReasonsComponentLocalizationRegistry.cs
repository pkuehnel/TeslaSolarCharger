using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class NotChargingAtExpectedPowerReasonsComponentLocalizationRegistry : TextLocalizationRegistry<NotChargingAtExpectedPowerReasonsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("{0} reason why loadpoint charges with different power than you might expect",
            new TextLocalizationTranslation(LanguageCodes.English, "{0} reason why loadpoint charges with different power than you might expect"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} Grund, warum der Ladepunkt mit einer anderen Leistung lädt als erwartet"));

        Register("{0} reasons why loadpoint charges with different power than you might expect",
            new TextLocalizationTranslation(LanguageCodes.English, "{0} reasons why loadpoint charges with different power than you might expect"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} Gründe, warum der Ladepunkt mit einer anderen Leistung lädt als erwartet"));

        Register("remaining)",
            new TextLocalizationTranslation(LanguageCodes.English, "remaining)"),
            new TextLocalizationTranslation(LanguageCodes.German, "verbleibend)"));
    }
}
