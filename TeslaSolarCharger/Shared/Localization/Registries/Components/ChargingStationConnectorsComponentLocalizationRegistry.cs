using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class ChargingStationConnectorsComponentLocalizationRegistry : TextLocalizationRegistry<ChargingStationConnectorsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarSettingsCurrentBelowSixTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Current below 6A not recommended"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strom unter 6A nicht empfohlen"));

        Register(TranslationKeys.TheType2StandardStatesThat,
            new TextLocalizationTranslation(LanguageCodes.English, "The Type 2 standard states that the minimum current below 6A is not allowed. Setting this below 6A might result in unexpected behavior like the car not charging at all."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Typ-2-Standard sieht vor, dass Ströme unter 6A nicht zulässig sind. Werte unter 6A können zu unerwartetem Verhalten führen, etwa dass das Auto gar nicht lädt."));
    }
}
