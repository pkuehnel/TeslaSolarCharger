using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries;

public class CarOverviewSettingsPropertyLocalization : PropertyLocalizationRegistry<DtoCarOverviewSettings>
{
    protected override void Configure()
    {
        Register(x => x.MinSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Min SoC",
                "Always charge at full speed until this SoC even if there is not enough solar power"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Min-Ladestand",
                "Bis zu diesem Ladestand immer mit voller Leistung laden, auch wenn nicht genügend Solarstrom vorhanden ist."));

        Register(x => x.MaxSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max SoC",
                "Stop charging at this SoC even if there is enough solar power"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Max-Ladestand",
                "Bei diesem Ladestand den Ladevorgang stoppen, auch wenn genügend Solarstrom vorhanden ist."));
    }
}
