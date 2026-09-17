using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class PowerBufferComponentLocalizationRegistry : TextLocalizationRegistry<PowerBufferComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.PowerBufferUpdated,
            new TextLocalizationTranslation(LanguageCodes.English, "Power Buffer updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Leistungspuffer aktualisiert"));

        Register(TranslationKeys.PowerBufferUpdateFailed,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Power Buffer"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktualisierung des Leistungspuffers fehlgeschlagen"));
    }
}
