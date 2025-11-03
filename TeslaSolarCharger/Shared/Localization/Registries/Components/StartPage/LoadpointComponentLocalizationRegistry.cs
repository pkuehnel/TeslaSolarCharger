using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class LoadpointComponentLocalizationRegistry : TextLocalizationRegistry<LoadpointComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.Phase012A,
            new TextLocalizationTranslation(LanguageCodes.English, "Phase {0}: {1}/{2} A"),
            new TextLocalizationTranslation(LanguageCodes.German, "Phase {0}: {1}/{2} A"));
    }
}
