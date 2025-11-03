using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class CustomIconLocalizationRegistry : TextLocalizationRegistry<CustomIconLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CustomIconNotPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "Not "),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht "));
    }
}
