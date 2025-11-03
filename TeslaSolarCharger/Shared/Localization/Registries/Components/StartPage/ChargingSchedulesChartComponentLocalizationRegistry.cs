using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingSchedulesChartComponentLocalizationRegistry : TextLocalizationRegistry<ChargingSchedulesChartComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingSchedulesChartScheduledPowerLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Scheduled power"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geplante Leistung"));

        Register(TranslationKeys.ChargingSchedulesChartScheduledEnergyLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Scheduled Energy ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geplante Energie ({0} kWh)"));

        Register(TranslationKeys.ChargingSchedulesChartGridPriceLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis pro kWh"));

        Register(TranslationKeys.ChargingSchedulesChartAverageKwLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "avg. kW"),
            new TextLocalizationTranslation(LanguageCodes.German, "durchschn. kW"));

        Register(TranslationKeys.ChargingSchedulesChartGridPricePerKwhLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Gridprice / kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis / kWh"));
    }
}
