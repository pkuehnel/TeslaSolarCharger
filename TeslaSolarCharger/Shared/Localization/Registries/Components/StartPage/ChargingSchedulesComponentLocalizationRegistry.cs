namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingSchedulesComponentLocalizationRegistry : TextLocalizationRegistry<ChargingSchedulesComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Nothing planned",
            new TextLocalizationTranslation(LanguageCodes.English, "Nothing planned"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nichts geplant"));

        Register("Next planned charge starts at {0}",
            new TextLocalizationTranslation(LanguageCodes.English, "Next planned charge starts at {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nächster geplanter Ladevorgang beginnt um {0}"));

        Register("Schedule Reasons",
            new TextLocalizationTranslation(LanguageCodes.English, "Schedule Reasons"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladegrund"));

        Register("Valid From",
            new TextLocalizationTranslation(LanguageCodes.English, "Valid From"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig von"));

        Register("Valid To",
            new TextLocalizationTranslation(LanguageCodes.English, "Valid To"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig bis"));

        Register("Estimated Charging Power",
            new TextLocalizationTranslation(LanguageCodes.English, "Estimated Charging Power"),
            new TextLocalizationTranslation(LanguageCodes.German, "erwartete Ladeleistung"));

        Register("Enough solar power expected",
            new TextLocalizationTranslation(LanguageCodes.English, "Enough solar power expected"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ausreichend Solarstrom erwartet"));

        Register("Discharging home battery",
            new TextLocalizationTranslation(LanguageCodes.English, "Discharging home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Entladung des Heimspeichers"));

        Register("Cheap grid price",
            new TextLocalizationTranslation(LanguageCodes.English, "Cheap grid price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Günstiger Netzstrompreis"));

        Register("Bridge between schedules to reduce charge starts/stops",
            new TextLocalizationTranslation(LanguageCodes.English, "Bridge between schedules to reduce charge starts/stops"),
            new TextLocalizationTranslation(LanguageCodes.German, "Überbrückung zwischen Zeitplänen zur Reduzierung von Lade-Starts/-Stopps"));

        Register("Latest possible time",
            new TextLocalizationTranslation(LanguageCodes.English, "Latest possible time"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spätestmöglicher Zeitpunkt"));
    }
}
