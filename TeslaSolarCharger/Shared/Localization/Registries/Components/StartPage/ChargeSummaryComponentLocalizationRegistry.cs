using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargeSummaryComponentLocalizationRegistry : TextLocalizationRegistry<ChargeSummaryComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargeSummarySolarEnergy,
            new TextLocalizationTranslation(LanguageCodes.English, "Total charged solar energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geladene Solarenergie insgesamt"));

        Register(TranslationKeys.ChargeSummaryHomeBatteryEnergy,
            new TextLocalizationTranslation(LanguageCodes.English, "Total charged home battery energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geladene Heimbatterie-Energie insgesamt"));

        Register(TranslationKeys.ChargeSummaryGridEnergy,
            new TextLocalizationTranslation(LanguageCodes.English, "Total charged grid energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geladene Netzenergie insgesamt"));

        Register(TranslationKeys.ChargeSummaryCost,
            new TextLocalizationTranslation(LanguageCodes.English, "Total charge cost. Note: The charge costs are also updated automatically in the charges you find in TeslaMate. This update can take up to 10 minutes after a charge is completed."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gesamte Ladekosten. Hinweis: Die Ladekosten werden auch automatisch in den Ladevorgängen aktualisiert, die Sie in TeslaMate finden. Dieses Update kann bis zu 10 Minuten nach Abschluss eines Ladevorgangs dauern."));

        Register(TranslationKeys.ChargeSummaryPricePerKwh,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} pro kWh"));
    }
}
