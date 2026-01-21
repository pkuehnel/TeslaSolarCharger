using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class TimeSeriesChartPageLocalizationRegistry : TextLocalizationRegistry<TimeSeriesChartPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.TimeSeriesChartModuleTempMin,
            new TextLocalizationTranslation(LanguageCodes.English, "Module Temp Min"),
            new TextLocalizationTranslation(LanguageCodes.German, "Modultemperatur Min"));

        Register(TranslationKeys.TimeSeriesChartModuleTempMax,
            new TextLocalizationTranslation(LanguageCodes.English, "Module Temp Max"),
            new TextLocalizationTranslation(LanguageCodes.German, "Modultemperatur Max"));

        Register(TranslationKeys.TimeSeriesChartStateOfCharge,
            new TextLocalizationTranslation(LanguageCodes.English, "State Of Charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand"));

        Register(TranslationKeys.TimeSeriesChartStateOfChargeLimit,
            new TextLocalizationTranslation(LanguageCodes.English, "State Of Charge Limit"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand-Limit"));
    }
}
