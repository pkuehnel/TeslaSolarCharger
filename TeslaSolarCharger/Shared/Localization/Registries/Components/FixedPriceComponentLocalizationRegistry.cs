using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class FixedPriceComponentLocalizationRegistry : TextLocalizationRegistry<FixedPriceComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.FromHour,
            new TextLocalizationTranslation(LanguageCodes.English, "From Hour"),
            new TextLocalizationTranslation(LanguageCodes.German, "Von Stunde"));

        Register(TranslationKeys.FromMinute,
            new TextLocalizationTranslation(LanguageCodes.English, "From Minute"),
            new TextLocalizationTranslation(LanguageCodes.German, "Von Minute"));

        Register(TranslationKeys.ToHour,
            new TextLocalizationTranslation(LanguageCodes.English, "To Hour"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bis Stunde"));

        Register(TranslationKeys.ToMinute,
            new TextLocalizationTranslation(LanguageCodes.English, "To Minute"),
            new TextLocalizationTranslation(LanguageCodes.German, "Bis Minute"));
    }
}
