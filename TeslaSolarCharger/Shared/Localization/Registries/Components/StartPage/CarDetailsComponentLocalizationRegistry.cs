using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class CarDetailsComponentLocalizationRegistry : TextLocalizationRegistry<CarDetailsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarDetailsConnectedToServer,
            new TextLocalizationTranslation(LanguageCodes.English, "connected to server"),
            new TextLocalizationTranslation(LanguageCodes.German, "mit Server verbunden"));

        Register(TranslationKeys.CarDetailsAtHome,
            new TextLocalizationTranslation(LanguageCodes.English, "at home"),
            new TextLocalizationTranslation(LanguageCodes.German, "zuhause"));

        Register(TranslationKeys.CarDetailsPluggedIn,
            new TextLocalizationTranslation(LanguageCodes.English, "plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "eingesteckt"));

        Register(TranslationKeys.CarDetailsCharging,
            new TextLocalizationTranslation(LanguageCodes.English, "charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "lädt"));

        Register(TranslationKeys.CarDetailsSocLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "SoC: "),
            new TextLocalizationTranslation(LanguageCodes.German, "SoC: "));

        Register(TranslationKeys.CarDetailsCarLimitLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Limit: "),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuglimit: "));

        Register(TranslationKeys.CarDetailsManualSocWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "As this car is not connected via an API, you need to manually set the current state of charge. Note: Each time you plug in the car, the SoC is reset, as TSC does not know how much energy the car has used."),
            new TextLocalizationTranslation(LanguageCodes.German, "Da dieses Auto nicht über eine API verbunden ist, müssen Sie den aktuellen Ladestand manuell einstellen. Hinweis: Jedes Mal, wenn Sie das Auto anschließen, wird der SoC zurückgesetzt, da TSC nicht weiß, wie viel Energie das Auto verbraucht hat."));

        Register(TranslationKeys.CarDetailsNoSocOnNotManualCarWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Although this car is connected via an API, the current state of charge is unknown. Depending on the vehicle manufacturer, this can have various causes. Especially when a vehicle has been newly connected via API, it may take a few hours for data to be transmitted. The initial transmission can often be accelerated by moving the vehicle and consuming at least 2% of the battery charge. If no state of charge is displayed within 24 hours, please contact support@solar4car.com with your Vehicle Identification Number (VIN)."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dieses Auto ist zwar über eine API verbunden, der Ladestand ist aktuell jedoch unbekannt. Abhängig vom Fahrzeughersteller kann dies unterschiedliche Ursachen haben. Insbesondere wenn ein Fahrzeug neu per API verbunden wurde, kann es unter Umständen einige Stunden dauern, bis Daten übermittelt werden. Häufig lässt sich die Erstübermittlung beschleunigen, indem das Fahrzeug bewegt wird und mindestens 2 % der Akkuladung verbraucht werden. Sollte innerhalb von 24 Stunden kein Ladestand angezeigt werden, melden Sie sich bitte mit Ihrer Fahrgestellnummer unter support@solar4car.com."));

        Register(TranslationKeys.CarDetailsManualSocHelperText,
            new TextLocalizationTranslation(LanguageCodes.English, "You can manually enter a state of charge here. During a charging session, it will be updated automatically based on the charging power and battery capacity."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie können hier manuell einen Ladestand eintragen. Während eines Ladevorgangs wird dieser automatisch basierend auf Ladeleistung und Batteriekapazität aktualisiert."));

        Register(TranslationKeys.CarDetailsStateOfChargeLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "State of Charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand"));

        Register(TranslationKeys.CarDetailsChargeModeLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Mode"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus"));

        Register(TranslationKeys.CarDetailsManualModeTeslaHint,
            new TextLocalizationTranslation(LanguageCodes.English, "You need to manually wake up the car and start charging via the Tesla app. You can only change the current here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie müssen das Auto manuell aufwecken und den Ladevorgang über die Tesla-App starten. Sie können hier nur den Strom ändern."));

        Register(TranslationKeys.CarDetailsCurrentToSetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Current to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Einzustellender Strom"));

        Register(TranslationKeys.CarDetailsSetCurrentButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Current"),
            new TextLocalizationTranslation(LanguageCodes.German, "Strom setzen"));

        Register(TranslationKeys.CarDetailsManualModeNoOcppHint,
            new TextLocalizationTranslation(LanguageCodes.English, "Only Teslas or cars connected via an OCPP charging connector can be charged in Manual mode. Please connect this car to an OCPP charging connector."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nur Teslas oder Autos, die über einen OCPP-Ladeanschluss verbunden sind, können im manuellen Modus geladen werden. Bitte verbinden Sie dieses Auto mit einem OCPP-Ladeanschluss."));

        Register(TranslationKeys.CarDetailsMinSocUpdated,
            new TextLocalizationTranslation(LanguageCodes.English, "Min SOC updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Min. SoC erfolgreich aktualisiert."));

        Register(TranslationKeys.CarDetailsMaxSocUpdated,
            new TextLocalizationTranslation(LanguageCodes.English, "Max SOC updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Max. SoC erfolgreich aktualisiert."));

        Register(TranslationKeys.CarDetailsChargeModeUpdated,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Mode updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus erfolgreich aktualisiert."));

        Register(TranslationKeys.CarDetailsValidCurrentRequired,
            new TextLocalizationTranslation(LanguageCodes.English, "Please set a valid current."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte stellen Sie einen gültigen Strom ein."));

        Register(TranslationKeys.CarDetailsCommandSent,
            new TextLocalizationTranslation(LanguageCodes.English, "Command successfully sent"),
            new TextLocalizationTranslation(LanguageCodes.German, "Befehl erfolgreich gesendet"));

        Register(TranslationKeys.CarDetailsValidSocRequired,
            new TextLocalizationTranslation(LanguageCodes.English, "Please set a valid state of charge."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte stellen Sie einen gültigen Ladestand ein."));

        Register(TranslationKeys.CarDetailsSocRangeError,
            new TextLocalizationTranslation(LanguageCodes.English, "State of charge must be between 0 and 100%."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand muss zwischen 0 und 100% liegen."));

        Register(TranslationKeys.CarDetailsSocUpdated,
            new TextLocalizationTranslation(LanguageCodes.English, "State of charge updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand erfolgreich aktualisiert."));

        Register(TranslationKeys.CarDetailsFailedToUpdateMinSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Min SOC: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktualisierung des Min-SOC fehlgeschlagen: {0}"));

        Register(TranslationKeys.CarDetailsFailedToUpdateMaxSoc,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Max SOC: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktualisierung des Max-SOC fehlgeschlagen: {0}"));

        Register(TranslationKeys.CarDetailsFailedToUpdateChargeMode,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Charge Mode: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Aktualisierung des Lademodus fehlgeschlagen: {0}"));

        Register(TranslationKeys.CarDetailsErrorFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Error: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler: {0}"));
    }
}
