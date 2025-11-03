using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingSchedulesComponentLocalizationRegistry : TextLocalizationRegistry<ChargingSchedulesComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingSchedulesComponentNothingPlannedMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Nothing planned"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nichts geplant"));

        Register(TranslationKeys.ChargingSchedulesComponentNextChargeFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Next planned charge starts at {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nächster geplanter Ladevorgang beginnt um {0}"));

        Register(TranslationKeys.ChargingSchedulesComponentValidFromLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid From"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig von"));

        Register(TranslationKeys.ChargingSchedulesComponentValidToLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid To"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig bis"));

        Register(TranslationKeys.ChargingSchedulesComponentChargingPowerLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Power"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeleistung"));
    }
}
