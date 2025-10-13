using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class SharedComponentLocalizationRegistry : TextLocalizationRegistry<SharedComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Base Configuration",
            new TextLocalizationTranslation(LanguageCodes.English, "Base Configuration"),
            new TextLocalizationTranslation(LanguageCodes.German, "Basiskonfiguration"));
    }
}
