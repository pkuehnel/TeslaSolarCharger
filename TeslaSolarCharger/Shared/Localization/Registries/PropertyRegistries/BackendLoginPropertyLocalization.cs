using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries;

public class BackendLoginPropertyLocalization : PropertyLocalizationRegistry<DtoBackendLogin>
{
    protected override void Configure()
    {
        Register(x => x.EMail,
            new PropertyLocalizationTranslation(LanguageCodes.English, "E-Mail", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "E-Mail", null));

        Register(x => x.Password,
            new PropertyLocalizationTranslation(LanguageCodes.English, "Password", null),
            new PropertyLocalizationTranslation(LanguageCodes.German, "Passwort", null));
    }
}
