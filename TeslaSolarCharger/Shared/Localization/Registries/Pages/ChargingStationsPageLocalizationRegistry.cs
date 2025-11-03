using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Pages;

public class ChargingStationsPageLocalizationRegistry : TextLocalizationRegistry<ChargingStationsPageLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.SharedChargingStationsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging Stations"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen"));

        Register(TranslationKeys.HowToConnectANewCharging,
            new TextLocalizationTranslation(LanguageCodes.English, "How to connect a new Charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "So verbindest du eine neue Ladestation"));

        Register(TranslationKeys.ChargingStationsAreAddedAutomaticallyAs,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging stations are added automatically as soon as they connect via OCPP."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestationen werden automatisch hinzugefügt, sobald sie sich über OCPP verbinden."));

        Register(TranslationKeys.ToConnectSetTheOcppUrl,
            new TextLocalizationTranslation(LanguageCodes.English, "To connect, set the OCPP URL to the following: <code>ws://YOUR-TSC-IP:7190/api/Ocpp/</code> followed by a charging point ID."),
            new TextLocalizationTranslation(LanguageCodes.German, "Zum Verbinden setze die OCPP-URL wie folgt: <code>ws://DEINE-TSC-IP:7190/api/Ocpp/</code> gefolgt von einer Ladepunkt-ID."));

        Register(TranslationKeys.NoteManyChargingStationsAutomaticallyAdd,
            new TextLocalizationTranslation(LanguageCodes.English, "Note: Many charging stations automatically add a charging point ID to the url, just make sure, that the resulting URL looks similar to the following example. Mind the single <code>/</code> after <code>Ocpp</code>"),
            new TextLocalizationTranslation(LanguageCodes.German, "Hinweis: Viele Ladestationen fügen automatisch eine Ladepunkt-ID zur URL hinzu. Achte darauf, dass die resultierende URL wie im folgenden Beispiel aussieht. Beachte den einzelnen <code>/</code> nach <code>Ocpp</code>"));

        Register(TranslationKeys.CodeWs19216817836,
            new TextLocalizationTranslation(LanguageCodes.English, "<code>ws://192.168.178.36:7190/api/Ocpp/C00485L</code>"),
            new TextLocalizationTranslation(LanguageCodes.German, "<code>ws://192.168.178.36:7190/api/Ocpp/C00485L</code>"));

        Register(TranslationKeys.NoChargingStationsFound,
            new TextLocalizationTranslation(LanguageCodes.English, "No charging stations found"),
            new TextLocalizationTranslation(LanguageCodes.German, "Keine Ladestationen gefunden"));

        Register(TranslationKeys.ConnectedViaOcpp,
            new TextLocalizationTranslation(LanguageCodes.English, "Connected via OCPP"),
            new TextLocalizationTranslation(LanguageCodes.German, "Über OCPP verbunden"));

        Register(TranslationKeys.NotConnectedViaOcpp,
            new TextLocalizationTranslation(LanguageCodes.English, "Not connected via OCPP"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht über OCPP verbunden"));
    }
}
