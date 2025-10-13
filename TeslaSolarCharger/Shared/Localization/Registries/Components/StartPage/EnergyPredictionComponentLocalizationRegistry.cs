using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class EnergyPredictionComponentLocalizationRegistry : TextLocalizationRegistry<EnergyPredictionComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Date",
            new TextLocalizationTranslation(LanguageCodes.English, "Date"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datum"));

        Register("Battery Charging ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Charging ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterieladung ({0} kWh)"));

        Register("Home Battery SoC %",
            new TextLocalizationTranslation(LanguageCodes.English, "Home Battery SoC %"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeicher-SoC %"));

        Register("Battery Discharge ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Discharge ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterieentladung ({0} kWh)"));

        Register("House Prediction ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "House Prediction ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausprognose ({0} kWh)"));

        Register("House Actual ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "House Actual ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausverbrauch ({0} kWh)"));

        Register("Solar Prediction ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose ({0} kWh)"));

        Register("Solar Actual ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Actual ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarerzeugung ({0} kWh)"));

        Register("Grid Export ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Export ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzeinspeisung ({0} kWh)"));

        Register("Grid Import ({0} kWh)",
            new TextLocalizationTranslation(LanguageCodes.English, "Grid Import ({0} kWh)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Netzbezug ({0} kWh)"));

        Register("kWh",
            new TextLocalizationTranslation(LanguageCodes.English, "kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "kWh"));

        Register("%",
            new TextLocalizationTranslation(LanguageCodes.English, "%"),
            new TextLocalizationTranslation(LanguageCodes.German, "%"));

        Register("Solar Actual",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Actual"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarerzeugung"));

        Register("House Prediction",
            new TextLocalizationTranslation(LanguageCodes.English, "House Prediction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausprognose"));

        Register("House Actual",
            new TextLocalizationTranslation(LanguageCodes.English, "House Actual"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausverbrauch"));

        Register("Battery Charged",
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Charged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie geladen"));

        Register("Battery Discharged",
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Discharged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie entladen"));

        Register("To Grid",
            new TextLocalizationTranslation(LanguageCodes.English, "To Grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ins Netz"));

        Register("From Grid",
            new TextLocalizationTranslation(LanguageCodes.English, "From Grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aus dem Netz"));

        Register("Solar Prediction from now",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction from now"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose ab jetzt"));

        Register("Solar Prediction",
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose"));

        Register("Cannot select a date more than {0} day(s) in the future",
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot select a date more than {0} day(s) in the future"),
            new TextLocalizationTranslation(LanguageCodes.German, "Es kann kein Datum gewählt werden, das mehr als {0} Tag(e) in der Zukunft liegt"));
    }
}
