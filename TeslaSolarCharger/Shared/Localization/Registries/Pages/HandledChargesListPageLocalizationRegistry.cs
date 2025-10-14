using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class HandledChargesListPageLocalizationRegistry : TextLocalizationRegistry<HandledChargesListPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("Handled Charges",
            new TextLocalizationTranslation(LanguageCodes.English, "Handled Charges"),
            new TextLocalizationTranslation(LanguageCodes.German, "Verarbeitete Ladevorgänge"));
    }
}
