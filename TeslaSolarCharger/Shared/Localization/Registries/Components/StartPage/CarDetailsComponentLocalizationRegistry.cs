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

        Register(TranslationKeys.CarDetailsFleetTelemetryWarningTime,
            new TextLocalizationTranslation(LanguageCodes.English, "Your TSC is connected to the server for less than 10 minutes. As Fleet Telemetry only sends states every 10 minutes the data shown here might not be up-to-date. If any data here is not correct, just wait for 10 minutes without restarting TSC. Note: This is a normal bahavior after a restart of the TSC. If all car data is displayed correctly, you can ignore this message."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr TSC ist seit weniger als 10 Minuten mit dem Server verbunden. Da Fleet Telemetry Zustände nur alle 10 Minuten sendet, sind die hier angezeigten Daten möglicherweise nicht aktuell. Wenn Daten nicht korrekt sind, warten Sie einfach 10 Minuten, ohne TSC neu zu starten. Hinweis: Dies ist ein normales Verhalten nach einem Neustart von TSC. Wenn alle Fahrzeugdaten korrekt angezeigt werden, können Sie diese Meldung ignorieren."));

        Register(TranslationKeys.CarDetailsFleetTelemetryWarningSleep,
            new TextLocalizationTranslation(LanguageCodes.English, "Your car went to sleep within 10 minutes after the TSC connected to the server. If you see wrong data here, please wake up your car via the Tesla app or by opening a door."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Auto ist innerhalb von 10 Minuten nach der Verbindung von TSC mit dem Server eingeschlafen. Wenn Sie hier falsche Daten sehen, wecken Sie bitte Ihr Auto über die Tesla-App oder durch Öffnen einer Tür auf."));

        Register(TranslationKeys.CarDetailsSocLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "SoC: "),
            new TextLocalizationTranslation(LanguageCodes.German, "SoC: "));

        Register(TranslationKeys.CarDetailsCarLimitLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Limit: "),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeuglimit: "));

        Register(TranslationKeys.CarDetailsManualSocWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "As this car is not connected via an API you need to manually set the current state of charge. Note: Each time you plugin the car the SoC is reset as TSC does not know how much energy the car used."),
            new TextLocalizationTranslation(LanguageCodes.German, "Da dieses Auto nicht über eine API verbunden ist, müssen Sie den aktuellen Ladestand manuell einstellen. Hinweis: Jedes Mal, wenn Sie das Auto anschließen, wird der SoC zurückgesetzt, da TSC nicht weiß, wie viel Energie das Auto verbraucht hat."));

        Register(TranslationKeys.CarDetailsStateOfChargeLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "State of Charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand"));

        Register(TranslationKeys.CarDetailsChargeModeLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Mode"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus"));

        Register(TranslationKeys.CarDetailsChargeModeInfoLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Mode Info"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus Info"));

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
    }
}
