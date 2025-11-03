using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class EditFormComponentLocalizationRegistry : TextLocalizationRegistry<EditFormComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.SharedProcessing,
            new TextLocalizationTranslation(LanguageCodes.English, "Processing"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wird verarbeitet"));
    }
}
