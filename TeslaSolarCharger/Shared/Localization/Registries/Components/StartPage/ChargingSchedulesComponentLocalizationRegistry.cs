using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargingSchedulesComponentLocalizationRegistry : TextLocalizationRegistry<ChargingSchedulesComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargingSchedulesNothingPlanned,
            new TextLocalizationTranslation(LanguageCodes.English, "Nothing planned"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nichts geplant"));

        Register(TranslationKeys.ChargingSchedulesNextStart,
            new TextLocalizationTranslation(LanguageCodes.English, "Next planned charge starts at {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nächster geplanter Ladevorgang startet um {0}"));

        Register(TranslationKeys.ChargingSchedulesReasonSolar,
            new TextLocalizationTranslation(LanguageCodes.English, "Enough solar power expected"),
            new TextLocalizationTranslation(LanguageCodes.German, "Genügend Solarstrom erwartet"));

        Register(TranslationKeys.ChargingSchedulesReasonBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Discharging home battery"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hausbatterie entladen"));

        Register(TranslationKeys.ChargingSchedulesReasonGridPrice,
            new TextLocalizationTranslation(LanguageCodes.English, "Cheap grid price"),
            new TextLocalizationTranslation(LanguageCodes.German, "Günstiger Netzpreis"));

        Register(TranslationKeys.ChargingSchedulesReasonBridge,
            new TextLocalizationTranslation(LanguageCodes.English, "Bridge between schedules to reduce charge starts/stops"),
            new TextLocalizationTranslation(LanguageCodes.German, "Überbrückung zwischen Ladeplänen um Ladestarts/-stopps zu reduzieren"));

        Register(TranslationKeys.ChargingSchedulesReasonLatestTime,
            new TextLocalizationTranslation(LanguageCodes.English, "Latest possible time"),
            new TextLocalizationTranslation(LanguageCodes.German, "Spätester möglicher Zeitpunkt"));

        Register(TranslationKeys.ChargingScheduleHeaderScheduleReasons,
            new TextLocalizationTranslation(LanguageCodes.English, "Schedule Reasons"),
            new TextLocalizationTranslation(LanguageCodes.German, "Planungsgründe"));

        Register(TranslationKeys.ChargingScheduleHeaderValidFrom,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid From"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig Ab"));

        Register(TranslationKeys.ChargingScheduleHeaderValidTo,
            new TextLocalizationTranslation(LanguageCodes.English, "Valid To"),
            new TextLocalizationTranslation(LanguageCodes.German, "Gültig Bis"));

        Register(TranslationKeys.ChargingScheduleHeaderEstimatedChargingPower,
            new TextLocalizationTranslation(LanguageCodes.English, "Estimated Charging Power"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geschätzte Ladeleistung"));
    }
}
