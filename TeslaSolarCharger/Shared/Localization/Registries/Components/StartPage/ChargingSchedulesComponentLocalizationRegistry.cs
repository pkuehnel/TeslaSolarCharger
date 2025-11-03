using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingSchedulesComponentLocalizationRegistry : TextLocalizationRegistry<ChargingSchedulesComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.NothingPlanned,
            new TextLocalizationTranslation(LanguageCodes.English, "Nothing planned"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nichts geplant"));

        Register(TranslationKeys.NextPlannedChargeStartsAt0,
            new TextLocalizationTranslation(LanguageCodes.English, "Next planned charge starts at {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nächster geplanter Ladevorgang beginnt um {0}"));

        Register(TranslationKeys.ValidFrom,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid From"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig von"));

        Register(TranslationKeys.ValidTo,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid To"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig bis"));

        Register(TranslationKeys.ChargingPower,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Power"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeleistung"));
    }
}
