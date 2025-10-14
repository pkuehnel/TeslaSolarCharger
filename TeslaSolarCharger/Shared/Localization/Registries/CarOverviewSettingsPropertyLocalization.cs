using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries;

public class CarOverviewSettingsPropertyLocalization : PropertyLocalizationRegistry<DtoCarOverviewSettings>
{
    protected override void Configure()
    {
        Register(x => x.MinSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Min Soc",
                "Always charge at full speed until this soc even if there is not enough solar power"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Min-Ladestand",
                "Bis zu diesem Ladestand immer mit voller Leistung laden, auch wenn nicht genügend Solarstrom vorhanden ist."));

        Register(x => x.MaxSoc,
            new PropertyLocalizationTranslation(LanguageCodes.English,
                "Max Soc",
                "Stop charging at this soc even if there is enough solar power"),
            new PropertyLocalizationTranslation(LanguageCodes.German,
                "Max-Ladestand",
                "Bei diesem Ladestand den Ladevorgang stoppen, auch wenn genügend Solarstrom vorhanden ist."));
    }
}
