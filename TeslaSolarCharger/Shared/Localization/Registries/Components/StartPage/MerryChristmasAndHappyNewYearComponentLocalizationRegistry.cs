using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class MerryChristmasAndHappyNewYearComponentLocalizationRegistry : TextLocalizationRegistry<MerryChristmasAndHappyNewYearComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.MerryChristmasComponentTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Merry Christmas! &#127876; &#127877; &#127873;"),
            new TextLocalizationTranslation(LanguageCodes.German, "Frohe Weihnachten! &#127876; &#127877; &#127873;"));

        Register(TranslationKeys.MerryChristmasComponentHappyNewYear,
            new TextLocalizationTranslation(LanguageCodes.English, "Happy New Year! &#127878; &#127881; &#127882;"),
            new TextLocalizationTranslation(LanguageCodes.German, "Frohes neues Jahr! &#127878; &#127881; &#127882;"));
    }
}
