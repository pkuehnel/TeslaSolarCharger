using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class AutoReloadOnVersionChangeComponentLocalizationRegistry : TextLocalizationRegistry<AutoReloadOnVersionChangeComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ANewVersionOfTheApplication,
            new TextLocalizationTranslation(LanguageCodes.English, "A new version of the application is available. The application will now reload to update to the latest version."),
            new TextLocalizationTranslation(LanguageCodes.German, "Eine neue Version der Anwendung ist verfügbar. Die Anwendung wird jetzt neu geladen, um auf die aktuelle Version zu aktualisieren."));
    }
}
