using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingSchedulesChartComponentLocalizationRegistry : TextLocalizationRegistry<ChargingSchedulesChartComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Scheduled power",
            new TextLocalizationTranslation(LanguageCodes.English, "Scheduled power"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geplante Leistung"));

        Register("Scheduled Energy ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "Scheduled Energy ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geplante Energie ({0} kWh)"));

        Register("Grid Price per kWh",
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Price per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis pro kWh"));

        Register("avg. kW",
            new TextLocalizationTranslation(LanguageCodes.English, "avg. kW"),
            new TextLocalizationTranslation(LanguageCodes.German, "durchschn. kW"));

        Register("Gridprice / kWh",
            new TextLocalizationTranslation(LanguageCodes.English, "Gridprice / kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzpreis / kWh"));
    }
}
