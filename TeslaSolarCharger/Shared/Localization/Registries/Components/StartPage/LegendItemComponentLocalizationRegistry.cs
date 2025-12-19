using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class LegendItemComponentLocalizationRegistry : TextLocalizationRegistry<LegendItemComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.LegendItemUnitKwh,
            new TextLocalizationTranslation(LanguageCodes.English, "kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "kWh"));
    }
}
