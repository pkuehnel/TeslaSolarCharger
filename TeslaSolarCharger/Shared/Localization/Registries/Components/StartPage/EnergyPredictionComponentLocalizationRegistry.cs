using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class EnergyPredictionComponentLocalizationRegistry : TextLocalizationRegistry<EnergyPredictionComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.Date,
            new TextLocalizationTranslation(LanguageCodes.English, "Date"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datum"));

        Register(TranslationKeys.BatteryCharging0Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Charging ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterieladung ({0} kWh)"));

        Register(TranslationKeys.HomeBatterySoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Battery SoC %"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeicher-Ladestand %"));

        Register(TranslationKeys.BatteryDischarge0Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Discharge ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterieentladung ({0} kWh)"));

        Register(TranslationKeys.HousePrediction0Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "House Prediction ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausprognose ({0} kWh)"));

        Register(TranslationKeys.HouseActual0Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "House Actual ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausverbrauch ({0} kWh)"));

        Register(TranslationKeys.SolarPrediction0Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose ({0} kWh)"));

        Register(TranslationKeys.SolarActual0Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Actual ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarerzeugung ({0} kWh)"));

        Register(TranslationKeys.GridExport0Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Export ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzeinspeisung ({0} kWh)"));

        Register(TranslationKeys.GridImport0Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Import ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzbezug ({0} kWh)"));

        Register(TranslationKeys.Kwh,
            new TextLocalizationTranslation(LanguageCodes.English, "kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "kWh"));

        Register(TranslationKeys.Key,
            new TextLocalizationTranslation(LanguageCodes.English, "%"),
            new TextLocalizationTranslation(LanguageCodes.German, "%"));

        Register(TranslationKeys.SolarActual,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Actual"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarerzeugung"));

        Register(TranslationKeys.HousePrediction,
            new TextLocalizationTranslation(LanguageCodes.English, "House Prediction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausprognose"));

        Register(TranslationKeys.HouseActual,
            new TextLocalizationTranslation(LanguageCodes.English, "House Actual"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausverbrauch"));

        Register(TranslationKeys.BatteryCharged,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Charged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie geladen"));

        Register(TranslationKeys.BatteryDischarged,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Discharged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie entladen"));

        Register(TranslationKeys.ToGrid,
            new TextLocalizationTranslation(LanguageCodes.English, "To Grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ins Netz"));

        Register(TranslationKeys.FromGrid,
            new TextLocalizationTranslation(LanguageCodes.English, "From Grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus dem Netz"));

        Register(TranslationKeys.SolarPredictionFromNow,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction from now"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose ab jetzt"));

        Register(TranslationKeys.SolarPrediction,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose"));

        Register(TranslationKeys.CannotSelectADateMoreThan,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot select a date more than {0} day(s) in the future"),
            new TextLocalizationTranslation(LanguageCodes.German, "Es kann kein Datum gewählt werden, das mehr als {0} Tag(e) in der Zukunft liegt"));
    }
}
