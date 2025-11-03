using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class CustomIconLocalizationRegistry : TextLocalizationRegistry<CustomIconLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.Not,
            new TextLocalizationTranslation(LanguageCodes.English, "Not "),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht "));
    }
}
