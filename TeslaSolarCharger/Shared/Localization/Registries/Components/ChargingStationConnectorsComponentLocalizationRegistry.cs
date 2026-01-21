using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class ChargingStationConnectorsComponentLocalizationRegistry : TextLocalizationRegistry<ChargingStationConnectorsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingStationConnectorCurrentWarningTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Current below 6A not recommended"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke unter 6A nicht empfohlen"));

        Register(TranslationKeys.ChargingStationConnectorCurrentWarningContent,
            new TextLocalizationTranslation(LanguageCodes.English, "The Type 2 standard states that the minimum current below 6A is not allowed. Setting this below 6A might result in unexpected behavior like the car not charging at all."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Typ-2-Standard besagt, dass ein Mindeststrom unter 6A nicht zulässig ist. Wenn Sie diesen Wert unter 6A einstellen, kann dies zu unerwartetem Verhalten führen, z. B. dass das Auto gar nicht lädt."));

        Register(TranslationKeys.ChargingStationConnectorSaved,
            new TextLocalizationTranslation(LanguageCodes.English, "Saved."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gespeichert."));
    }
}
