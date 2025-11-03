using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class PowerBufferComponentLocalizationRegistry : TextLocalizationRegistry<PowerBufferComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.PowerBufferUpdated,
            new TextLocalizationTranslation(LanguageCodes.English, "Power Buffer updated"),
            new TextLocalizationTranslation(LanguageCodes.German, "Power-Puffer aktualisiert"));

        Register(TranslationKeys.FailedToUpdatePowerBuffer,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Power Buffer"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktualisierung des Power-Puffers fehlgeschlagen"));
    }
}
