using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class EnergyPredictionComponentLocalizationRegistry : TextLocalizationRegistry<EnergyPredictionComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.EnergyPredictionDateLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Date"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datum"));

        Register(TranslationKeys.EnergyPredictionBatterySocSeries,
            new TextLocalizationTranslation(LanguageCodes.English, "Home Battery SoC %"),
            new TextLocalizationTranslation(LanguageCodes.German, "Heimspeicher SoC %"));

        Register(TranslationKeys.EnergyPredictionUnitKwh,
            new TextLocalizationTranslation(LanguageCodes.English, "kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "kWh"));

        Register(TranslationKeys.EnergyPredictionUnitPercent,
            new TextLocalizationTranslation(LanguageCodes.English, "%"),
            new TextLocalizationTranslation(LanguageCodes.German, "%"));

        Register(TranslationKeys.EnergyPredictionSolarPredictionFromNow,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction from now"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose ab jetzt"));

        Register(TranslationKeys.EnergyPredictionSolarPrediction,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Prediction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solarprognose"));

        Register(TranslationKeys.EnergyPredictionSolarActual,
            new TextLocalizationTranslation(LanguageCodes.English, "Solar Actual"),
            new TextLocalizationTranslation(LanguageCodes.German, "Solar Ist"));

        Register(TranslationKeys.EnergyPredictionHousePrediction,
            new TextLocalizationTranslation(LanguageCodes.English, "House Prediction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausverbrauchsprognose"));

        Register(TranslationKeys.EnergyPredictionHouseActual,
            new TextLocalizationTranslation(LanguageCodes.English, "House Actual"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausverbrauch Ist"));

        Register(TranslationKeys.EnergyPredictionBatteryCharged,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Charged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie geladen"));

        Register(TranslationKeys.EnergyPredictionBatteryDischarged,
            new TextLocalizationTranslation(LanguageCodes.English, "Battery Discharged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Batterie entladen"));

        Register(TranslationKeys.EnergyPredictionToGrid,
            new TextLocalizationTranslation(LanguageCodes.English, "To Grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ins Netz"));

        Register(TranslationKeys.EnergyPredictionFromGrid,
            new TextLocalizationTranslation(LanguageCodes.English, "From Grid"),
            new TextLocalizationTranslation(LanguageCodes.German, "Vom Netz"));

        Register(TranslationKeys.EnergyPredictionDateFutureError,
            new TextLocalizationTranslation(LanguageCodes.English, "Cannot select a date more than {0} day(s) in the future"),
            new TextLocalizationTranslation(LanguageCodes.German, "Datum darf nicht mehr als {0} Tag(e) in der Zukunft liegen"));
    }
}
