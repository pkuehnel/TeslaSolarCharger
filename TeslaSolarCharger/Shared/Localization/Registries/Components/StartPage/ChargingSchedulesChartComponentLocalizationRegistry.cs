using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingSchedulesChartComponentLocalizationRegistry : TextLocalizationRegistry<ChargingSchedulesChartComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingSchedulesChartTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Scheduled power"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geplante Leistung"));

        Register(TranslationKeys.ChargingSchedulesChartSeriesName,
            new TextLocalizationTranslation(LanguageCodes.English, "Scheduled Energy ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geplante Energie ({0} kWh)"));

        Register(TranslationKeys.ChargingSchedulesChartGridPriceSeriesName,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis pro kWh"));

        Register(TranslationKeys.ChargingSchedulesChartYAxisPower,
            new TextLocalizationTranslation(LanguageCodes.English, "avg. kW"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ø kW"));

        Register(TranslationKeys.ChargingSchedulesChartYAxisPrice,
            new TextLocalizationTranslation(LanguageCodes.English, "Gridprice / kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis / kWh"));
    }
}
