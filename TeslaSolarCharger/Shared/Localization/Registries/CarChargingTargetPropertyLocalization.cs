using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class CarChargingTargetPropertyLocalization : PropertyLocalizationRegistry<DtoCarChargingTarget>
{
    protected override void Configure()
    {
        Register(x => x.TargetSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Ziel-Ladestand",
                null));

        Register(x => x.DischargeHomeBatteryToMinSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Discharge Home Battery To Min Soc",
                "If no Target soc is set, TSC tries to discharge the home battery to its minimum SoC by the target time. If a Target Soc is set, TSC schedules charging to reduce grid usage by reducing the charging speed which your home battery is capable of."),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Heimbatterie auf Mindest-Ladestand entladen",
                "Wenn kein Ziel-Ladestand gesetzt ist, versucht TSC, die Heimbatterie bis zur Zielzeit auf ihren minimalen Ladestand zu entladen. Ist ein Ziel-Ladestand definiert, plant TSC das Laden so, dass der Netzbezug reduziert wird und nur die von der Heimbatterie unterstützte Ladeleistung genutzt wird."));

        Register(x => x.TargetDate,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Ziel-Datum",
                null));

        Register(x => x.TargetTime,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                null,
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Ziel-Zeit",
                null));

        Register(x => x.RepeatOnMondays,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Mo",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Mo",
                null));

        Register(x => x.RepeatOnTuesdays,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Tu",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Di",
                null));

        Register(x => x.RepeatOnWednesdays,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "We",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Mi",
                null));

        Register(x => x.RepeatOnThursdays,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Th",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Do",
                null));

        Register(x => x.RepeatOnFridays,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Fr",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Fr",
                null));

        Register(x => x.RepeatOnSaturdays,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Sa",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Sa",
                null));

        Register(x => x.RepeatOnSundays,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Su",
                null),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "So",
                null));
    }
}
