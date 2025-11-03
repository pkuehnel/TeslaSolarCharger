using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;

public class CarDetailsComponentLocalizationRegistry : TextLocalizationRegistry<CarDetailsComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarDetailsConnectedStatus,
            new TextLocalizationTranslation(LanguageCodes.English, "connected to server"),
            new TextLocalizationTranslation(LanguageCodes.German, "mit Server verbunden"));

        Register(TranslationKeys.CarDetailsAtHomeStatus,
            new TextLocalizationTranslation(LanguageCodes.English, "at home"),
            new TextLocalizationTranslation(LanguageCodes.German, "zu Hause"));

        Register(TranslationKeys.CarDetailsPluggedInStatus,
            new TextLocalizationTranslation(LanguageCodes.English, "plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "eingesteckt"));

        Register(TranslationKeys.CarDetailsChargingStatus,
            new TextLocalizationTranslation(LanguageCodes.English, "charging"),
            new TextLocalizationTranslation(LanguageCodes.German, "lädt"));

        Register(TranslationKeys.CarDetailsInitialSyncInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "Your TSC is connected to the server for less than 10 minutes. As Fleet Telemetry only sends states every 10 minutes the data shown here might not be up-to-date. If any data here is not correct, just wait for 10 minutes without restarting TSC. Note: This is a normal bahavior after a restart of the TSC. If all car data is displayed correctly, you can ignore this message."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dein TSC ist seit weniger als 10 Minuten mit dem Server verbunden. Da die Fleet-Telemetrie nur alle 10 Minuten Zustände sendet, sind die hier angezeigten Daten möglicherweise nicht aktuell. Wenn Daten nicht stimmen, warte 10 Minuten, ohne den TSC neu zu starten. Hinweis: Dies ist nach einem Neustart des TSC normales Verhalten. Wenn alle Fahrzeugdaten korrekt angezeigt werden, kannst du diese Meldung ignorieren."));

        Register(TranslationKeys.CarDetailsCarSleepInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "Your car went to sleep within 10 minutes after the TSC connected to the server. If you see wrong data here, please wake up your car via the Tesla app or by opening a door."),
            new TextLocalizationTranslation(LanguageCodes.German, "Dein Auto ist innerhalb von 10 Minuten eingeschlafen, nachdem sich der TSC mit dem Server verbunden hat. Wenn hier falsche Daten angezeigt werden, wecke dein Auto über die Tesla-App oder durch Öffnen einer Tür auf."));

        Register(TranslationKeys.CarDetailsManualSocInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "As this car is not connected via an API you need to manually set the current state of charge. Note: Each time you plugin the car the SoC is reset as TSC does not know how much energy the car used."),
            new TextLocalizationTranslation(LanguageCodes.German, "Da dieses Auto nicht über eine API verbunden ist, musst du den aktuellen Ladezustand manuell setzen. Hinweis: Jedes Mal, wenn du das Auto einsteckst, wird der Ladestand zurückgesetzt, da der TSC nicht weiß, wie viel Energie das Auto verbraucht hat."));

        Register(TranslationKeys.CarDetailsSocLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "State of Charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladezustand"));

        Register(TranslationKeys.CarDetailsChargeModeLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Mode"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus"));

        Register(TranslationKeys.CarDetailsManualChargeInstructions,
            new TextLocalizationTranslation(LanguageCodes.English, "You need to manually wake up the car and start charging via the Tesla app. You can only change the current here."),
            new TextLocalizationTranslation(LanguageCodes.German, "Du musst das Auto manuell wecken und über die Tesla-App mit dem Laden beginnen. Hier kannst du nur die Stromstärke ändern."));

        Register(TranslationKeys.CarDetailsCurrentToSetLabel,
            new TextLocalizationTranslation(LanguageCodes.English, "Current to set"),
            new TextLocalizationTranslation(LanguageCodes.German, "Zu setzende Stromstärke"));

        Register(TranslationKeys.CarDetailsSetCurrentButton,
            new TextLocalizationTranslation(LanguageCodes.English, "Set Current"),
            new TextLocalizationTranslation(LanguageCodes.German, "Stromstärke setzen"));

        Register(TranslationKeys.CarDetailsManualModeUnsupportedInfo,
            new TextLocalizationTranslation(LanguageCodes.English, "Only Teslas or cars connected via an OCPP charging connector can be charged in Manual mode. Please connect this car to an OCPP charging connector."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nur Teslas oder Fahrzeuge, die über einen OCPP-Ladeanschluss verbunden sind, können im manuellen Modus geladen werden. Bitte verbinde dieses Auto mit einem OCPP-Ladeanschluss."));

        Register(TranslationKeys.CarDetailsSocPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "SoC: "),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladestand: "));

        Register(TranslationKeys.CarDetailsCarLimitPrefix,
            new TextLocalizationTranslation(LanguageCodes.English, "Car Limit: "),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladelimit: "));

        Register(TranslationKeys.CarDetailsUpdateMinSocError,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Min SOC: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Minimaler Ladestand konnte nicht aktualisiert werden: {0}"));

        Register(TranslationKeys.CarDetailsUpdateMinSocSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "Min SOC updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Minimaler Ladestand erfolgreich aktualisiert."));

        Register(TranslationKeys.CarDetailsUpdateMaxSocError,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Max SOC: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Maximaler Ladestand konnte nicht aktualisiert werden: {0}"));

        Register(TranslationKeys.CarDetailsUpdateMaxSocSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "Max SOC updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Maximaler Ladestand erfolgreich aktualisiert."));

        Register(TranslationKeys.CarDetailsUpdateChargeModeError,
            new TextLocalizationTranslation(LanguageCodes.English, "Failed to update Charge Mode: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus konnte nicht aktualisiert werden: {0}"));

        Register(TranslationKeys.CarDetailsUpdateChargeModeSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge Mode updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus erfolgreich aktualisiert."));

        Register(TranslationKeys.CarDetailsInvalidCurrentWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Please set a valid current."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte gib eine gültige Stromstärke an."));

        Register(TranslationKeys.CarDetailsGenericErrorFormat,
            new TextLocalizationTranslation(LanguageCodes.English, "Error: {0}"),
            new TextLocalizationTranslation(LanguageCodes.German, "Fehler: {0}"));

        Register(TranslationKeys.CarDetailsCommandSuccessMessage,
            new TextLocalizationTranslation(LanguageCodes.English, "Command successfully sent"),
            new TextLocalizationTranslation(LanguageCodes.German, "Befehl erfolgreich gesendet"));

        Register(TranslationKeys.CarDetailsInvalidSocWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "Please set a valid state of charge."),
            new TextLocalizationTranslation(LanguageCodes.German, "Bitte gib einen gültigen Ladezustand an."));

        Register(TranslationKeys.CarDetailsSocRangeWarning,
            new TextLocalizationTranslation(LanguageCodes.English, "State of charge must be between 0 and 100%."),
            new TextLocalizationTranslation(LanguageCodes.German, "Der Ladezustand muss zwischen 0 und 100 % liegen."));

        Register(TranslationKeys.CarDetailsSocUpdateSuccess,
            new TextLocalizationTranslation(LanguageCodes.English, "State of charge updated successfully."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladezustand erfolgreich aktualisiert."));
    }
}
