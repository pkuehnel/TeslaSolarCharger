using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class CarControlCapabilityComponentLocalizationRegistry : TextLocalizationRegistry<CarControlCapabilityComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarControlCapabilityTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "How can TeslaSolarCharger reach your car?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie kann TeslaSolarCharger Ihr Auto erreichen?"));

        Register(TranslationKeys.CarControlCapabilityIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "To charge with your solar power, TeslaSolarCharger has to start, stop and adjust charging, and it needs to know how full the battery is. Which ways there are depends on your car. Each option says what it needs and what it costs."),
            new TextLocalizationTranslation(LanguageCodes.German, "Um mit Ihrem Solarstrom zu laden, muss TeslaSolarCharger das Laden starten, stoppen und anpassen und wissen, wie voll der Akku ist. Welche Wege es dafür gibt, hängt von Ihrem Auto ab. Bei jeder Option steht, was sie braucht und was sie kostet."));

        Register(TranslationKeys.CarControlCapabilityGroupTeslas,
            new TextLocalizationTranslation(LanguageCodes.English, "For a Tesla"),
            new TextLocalizationTranslation(LanguageCodes.German, "Für einen Tesla"));

        Register(TranslationKeys.CarControlCapabilityGroupOtherCars,
            new TextLocalizationTranslation(LanguageCodes.English, "For any other car"),
            new TextLocalizationTranslation(LanguageCodes.German, "Für jedes andere Auto"));
    }
}
