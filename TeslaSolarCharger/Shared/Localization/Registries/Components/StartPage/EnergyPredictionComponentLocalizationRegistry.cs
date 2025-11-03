using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class EnergyPredictionComponentLocalizationRegistry : TextLocalizationRegistry<EnergyPredictionComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.EnergyPredictionDateLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Date"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datum"));

        Register(TranslationKeys.EnergyPredictionBatteryChargingSeriesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Charging ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterieladung ({0} kWh)"));

        Register(TranslationKeys.EnergyPredictionHomeBatterySocLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Battery SoC %"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeicher-Ladestand %"));

        Register(TranslationKeys.EnergyPredictionBatteryDischargeSeriesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Discharge ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterieentladung ({0} kWh)"));

        Register(TranslationKeys.EnergyPredictionHousePredictionSeriesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "House Prediction ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausprognose ({0} kWh)"));

        Register(TranslationKeys.EnergyPredictionHouseActualSeriesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "House Actual ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausverbrauch ({0} kWh)"));

        Register(TranslationKeys.EnergyPredictionSolarPredictionSeriesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose ({0} kWh)"));

        Register(TranslationKeys.EnergyPredictionSolarActualSeriesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Actual ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarerzeugung ({0} kWh)"));

        Register(TranslationKeys.EnergyPredictionGridExportSeriesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Export ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzeinspeisung ({0} kWh)"));

        Register(TranslationKeys.EnergyPredictionGridImportSeriesTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Import ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzbezug ({0} kWh)"));

        Register(TranslationKeys.EnergyPredictionUnitKwh,
            new TextLocalizationTranslation(LanguageCodes.English, "kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "kWh"));

        Register(TranslationKeys.EnergyPredictionUnitPercent,
            new TextLocalizationTranslation(LanguageCodes.English, "%"),
            new TextLocalizationTranslation(LanguageCodes.German, "%"));

        Register(TranslationKeys.EnergyPredictionSolarActualLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Actual"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarerzeugung"));

        Register(TranslationKeys.EnergyPredictionHousePredictionLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "House Prediction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausprognose"));

        Register(TranslationKeys.EnergyPredictionHouseActualLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "House Actual"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausverbrauch"));

        Register(TranslationKeys.EnergyPredictionBatteryChargedLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Charged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie geladen"));

        Register(TranslationKeys.EnergyPredictionBatteryDischargedLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Discharged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie entladen"));

        Register(TranslationKeys.EnergyPredictionToGridLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "To Grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ins Netz"));

        Register(TranslationKeys.EnergyPredictionFromGridLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "From Grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus dem Netz"));

        Register(TranslationKeys.EnergyPredictionSolarPredictionFromNowLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction from now"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose ab jetzt"));

        Register(TranslationKeys.EnergyPredictionSolarPredictionLegend,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose"));

        Register(TranslationKeys.EnergyPredictionFutureDateLimitMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot select a date more than {0} day(s) in the future"),
            new TextLocalizationTranslation(LanguageCodes.German, "Es kann kein Datum gewählt werden, das mehr als {0} Tag(e) in der Zukunft liegt"));
    }
}
