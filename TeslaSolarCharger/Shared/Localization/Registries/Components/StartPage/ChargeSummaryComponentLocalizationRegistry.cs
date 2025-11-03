using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class ChargeSummaryComponentLocalizationRegistry : TextLocalizationRegistry<ChargeSummaryComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.ChargeSummarySolarEnergyLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Total charged solar energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geladene Solarenergie insgesamt"));

        Register(TranslationKeys.ChargeSummaryHomeBatteryEnergyLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Total charged home battery energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geladene Heimspeicherenergie insgesamt"));

        Register(TranslationKeys.ChargeSummaryGridEnergyLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Total charged grid energy"),
            new TextLocalizationTranslation(LanguageCodes.German, "Geladene Netzenergie insgesamt"));

        Register(TranslationKeys.ChargeSummaryTotalChargeCostDescription,
            new TextLocalizationTranslation(LanguageCodes.English, "Total Charge cost. Note: The charge costs are also autoupdated in the charges you find in TeslaMate. This update can take up to 10 minutes after a charge is completed."),
            new TextLocalizationTranslation(LanguageCodes.German, "Gesamte Ladekosten. Hinweis: Die Ladekosten werden auch automatisch in den Ladevorgängen aktualisiert, die du in TeslaMate findest. Dieses Update kann bis zu 10 Minuten nach Abschluss eines Ladevorgangs dauern."));

        Register(TranslationKeys.ChargeSummaryCostPerKwhFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "{0} per kWh"),
            new TextLocalizationTranslation(LanguageCodes.German, "{0} pro kWh"));
    }
}
