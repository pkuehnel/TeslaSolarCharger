using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Components;

public class CarChargingSetupComponentLocalizationRegistry : TextLocalizationRegistry<CarChargingSetupComponentLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.CarChargingSetupIntro,
            new TextLocalizationTranslation(LanguageCodes.English, "Answer a few questions and we'll show exactly what you need to set up."),
            new TextLocalizationTranslation(LanguageCodes.German, "Beantworten Sie ein paar Fragen und wir zeigen Ihnen genau, was Sie einrichten müssen."));

        Register(TranslationKeys.CarChargingSetupQuestionCarCount,
            new TextLocalizationTranslation(LanguageCodes.English, "How many electric cars do you have?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie viele Elektroautos haben Sie?"));

        Register(TranslationKeys.CarChargingSetupQuestionTeslaCount,
            new TextLocalizationTranslation(LanguageCodes.English, "How many of those are Teslas?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie viele davon sind Teslas?"));

        Register(TranslationKeys.CarChargingSetupQuestionStation,
            new TextLocalizationTranslation(LanguageCodes.English, "Do you have a charging station that supports OCPP?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Haben Sie eine Ladestation, die OCPP unterstützt?"));

        Register(TranslationKeys.CarChargingSetupQuestionBle,
            new TextLocalizationTranslation(LanguageCodes.English, "Can you place a Bluetooth device (e.g. a Raspberry Pi running TeslaSolarCharger) within a few metres of where your Tesla parks?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Können Sie ein Bluetooth-Gerät (z. B. einen Raspberry Pi mit TeslaSolarCharger) innerhalb weniger Meter vom Parkplatz Ihres Teslas aufstellen?"));

        Register(TranslationKeys.CarChargingSetupAnswerYes,
            new TextLocalizationTranslation(LanguageCodes.English, "Yes"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ja"));

        Register(TranslationKeys.CarChargingSetupAnswerNo,
            new TextLocalizationTranslation(LanguageCodes.English, "No"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nein"));

        Register(TranslationKeys.CarChargingSetupAnswerNotSure,
            new TextLocalizationTranslation(LanguageCodes.English, "Not sure"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht sicher"));

        Register(TranslationKeys.CarChargingSetupBleYes,
            new TextLocalizationTranslation(LanguageCodes.English, "Yes, I can"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ja, das kann ich"));

        Register(TranslationKeys.CarChargingSetupBleNo,
            new TextLocalizationTranslation(LanguageCodes.English, "No / not sure"),
            new TextLocalizationTranslation(LanguageCodes.German, "Nein / nicht sicher"));

        Register(TranslationKeys.CarChargingSetupPlanHeading,
            new TextLocalizationTranslation(LanguageCodes.English, "Your setup plan"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihr Einrichtungsplan"));

        Register(TranslationKeys.CarChargingSetupPlanNoCarsTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "No cars yet"),
            new TextLocalizationTranslation(LanguageCodes.German, "Noch keine Fahrzeuge"));

        Register(TranslationKeys.CarChargingSetupPlanNoCarsBody,
            new TextLocalizationTranslation(LanguageCodes.English, "You can add cars and a charging station here at any time — there's nothing to set up right now."),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie können hier jederzeit Fahrzeuge und eine Ladestation hinzufügen – im Moment gibt es nichts einzurichten."));

        Register(TranslationKeys.CarChargingSetupPlanTeslaBleTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Teslas — use Bluetooth (free)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Teslas – Bluetooth verwenden (kostenlos)"));

        Register(TranslationKeys.CarChargingSetupPlanTeslaBleBody,
            new TextLocalizationTranslation(LanguageCodes.English, "A device running TeslaSolarCharger talks to the car directly over a short-range Bluetooth link, so there is no monthly fee. This gives both charge control (start/stop and charging speed) and the battery level."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ein Gerät mit TeslaSolarCharger kommuniziert über eine Bluetooth-Verbindung mit kurzer Reichweite direkt mit dem Fahrzeug, daher fallen keine monatlichen Kosten an. Das ermöglicht sowohl die Ladesteuerung (Start/Stopp und Ladegeschwindigkeit) als auch das Auslesen des Ladestands."));

        Register(TranslationKeys.CarChargingSetupPlanTeslaApiTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Teslas — use Tesla's online API (€2.99/month)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Teslas – Teslas Online-API verwenden (2,99 €/Monat)"));

        Register(TranslationKeys.CarChargingSetupPlanTeslaApiBody,
            new TextLocalizationTranslation(LanguageCodes.English, "TeslaSolarCharger reaches the car over the internet through Tesla's servers, so it works from anywhere with no Bluetooth device needed. Tesla charges a fee (€2.99/month here). This gives both charge control and the battery level."),
            new TextLocalizationTranslation(LanguageCodes.German, "TeslaSolarCharger erreicht das Fahrzeug über das Internet via Teslas Server, funktioniert also von überall und ohne Bluetooth-Gerät. Tesla erhebt dafür eine Gebühr (hier 2,99 €/Monat). Das ermöglicht sowohl die Ladesteuerung als auch das Auslesen des Ladestands."));

        Register(TranslationKeys.CarChargingSetupPlanTeslaPendingTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Teslas — one more answer"),
            new TextLocalizationTranslation(LanguageCodes.German, "Teslas – noch eine Antwort"));

        Register(TranslationKeys.CarChargingSetupPlanTeslaPendingBody,
            new TextLocalizationTranslation(LanguageCodes.English, "Answer the Bluetooth question above to see the recommended way to connect your Tesla."),
            new TextLocalizationTranslation(LanguageCodes.German, "Beantworten Sie die Bluetooth-Frage oben, um die empfohlene Verbindungsart für Ihren Tesla zu sehen."));

        Register(TranslationKeys.CarChargingSetupPlanOtherStationTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Other car brands — controlled by your charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "Andere Fahrzeugmarken – über Ihre Ladestation gesteuert"));

        Register(TranslationKeys.CarChargingSetupPlanOtherStationBody,
            new TextLocalizationTranslation(LanguageCodes.English, "Non-Teslas are started, stopped, and throttled through the charging station. The station cannot read the battery level — optionally connect Smartcar (requires a license) to add it for smarter, battery-aware charging."),
            new TextLocalizationTranslation(LanguageCodes.German, "Nicht-Teslas werden über die Ladestation gestartet, gestoppt und gedrosselt. Die Station kann den Ladestand nicht auslesen – verbinden Sie optional Smartcar (lizenzpflichtig), um ihn für intelligenteres, ladestandsbewusstes Laden zu ergänzen."));

        Register(TranslationKeys.CarChargingSetupPlanOtherNoStationTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Other car brands — a charging station is required"),
            new TextLocalizationTranslation(LanguageCodes.German, "Andere Fahrzeugmarken – eine Ladestation ist erforderlich"));

        Register(TranslationKeys.CarChargingSetupPlanOtherNoStationBody,
            new TextLocalizationTranslation(LanguageCodes.English, "To control a non-Tesla, TeslaSolarCharger needs an OCPP-capable charging station. Without one it cannot start, stop, or adjust charging."),
            new TextLocalizationTranslation(LanguageCodes.German, "Um ein Nicht-Tesla-Fahrzeug zu steuern, benötigt TeslaSolarCharger eine OCPP-fähige Ladestation. Ohne diese kann es das Laden weder starten noch stoppen oder anpassen."));

        Register(TranslationKeys.CarChargingSetupPlanOtherNotSureExtra,
            new TextLocalizationTranslation(LanguageCodes.English, "Most modern smart wallboxes support OCPP — check your charger's settings or manual."),
            new TextLocalizationTranslation(LanguageCodes.German, "Die meisten modernen smarten Wallboxen unterstützen OCPP – prüfen Sie die Einstellungen oder das Handbuch Ihrer Ladestation."));

        Register(TranslationKeys.CarChargingSetupPlanOtherPendingTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "Other car brands — one more answer"),
            new TextLocalizationTranslation(LanguageCodes.German, "Andere Fahrzeugmarken – noch eine Antwort"));

        Register(TranslationKeys.CarChargingSetupPlanOtherPendingBody,
            new TextLocalizationTranslation(LanguageCodes.English, "Answer the charging-station question above to see what your other cars need."),
            new TextLocalizationTranslation(LanguageCodes.German, "Beantworten Sie die Frage zur Ladestation oben, um zu sehen, was Ihre anderen Fahrzeuge benötigen."));

        Register(TranslationKeys.CarChargingSetupPlanStationAlsoTitle,
            new TextLocalizationTranslation(LanguageCodes.English, "You also have a charging station"),
            new TextLocalizationTranslation(LanguageCodes.German, "Sie haben auch eine Ladestation"));

        Register(TranslationKeys.CarChargingSetupPlanStationAlsoBody,
            new TextLocalizationTranslation(LanguageCodes.English, "Your Teslas are already covered above, but you can connect the station too if you'd like to charge through it."),
            new TextLocalizationTranslation(LanguageCodes.German, "Ihre Teslas sind oben bereits abgedeckt, aber Sie können die Station ebenfalls verbinden, wenn Sie darüber laden möchten."));

        Register(TranslationKeys.CarChargingSetupHowItWorks,
            new TextLocalizationTranslation(LanguageCodes.English, "How does this work?"),
            new TextLocalizationTranslation(LanguageCodes.German, "Wie funktioniert das?"));
    }
}
