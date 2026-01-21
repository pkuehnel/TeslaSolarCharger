using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class CarSelectorComponentLocalizationRegistry : TextLocalizationRegistry<CarSelectorComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarSelectorConnectedCarLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Connected car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verbundenes Auto"));
    }
}
