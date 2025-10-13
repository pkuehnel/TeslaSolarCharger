using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Server;

public class NotChargingWithExpectedPowerReasonLocalizationRegistry : TextLocalizationRegistry<NotChargingWithExpectedPowerReasonLocalizationRegistry>
{
    protected override void Configure()
    {
        Register("OCPP connection not established. After a TSC or charger reboot it can take up to 5 minutes until the charger is connected again.",
            new TextLocalizationTranslation(LanguageCodes.English, "OCPP connection not established. After a TSC or charger reboot it can take up to 5 minutes until the charger is connected again."),
            new TextLocalizationTranslation(LanguageCodes.German, "OCPP-Verbindung nicht hergestellt. Nach einem Neustart von TSC oder der Wallbox kann es bis zu 5 Minuten dauern, bis die Wallbox wieder verbunden ist."));

        Register("Car is not at home",
            new TextLocalizationTranslation(LanguageCodes.English, "Car is not at home"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto befindet sich nicht zu Hause"));

        Register("Car is not plugged in",
            new TextLocalizationTranslation(LanguageCodes.English, "Car is not plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto ist nicht eingesteckt"));

        Register("Charging connector is not plugged in",
            new TextLocalizationTranslation(LanguageCodes.English, "Charging connector is not plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschluss ist nicht eingesteckt"));

        Register("Car is fully charged",
            new TextLocalizationTranslation(LanguageCodes.English, "Car is fully charged"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto ist vollständig geladen"));

        Register("Waiting phase switch cooldown time before starting to charge",
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting phase switch cooldown time before starting to charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Abkühlzeit für Phasenumschaltung, bevor mit dem Laden begonnen wird"));

        Register("Waiting for phase increase",
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for phase increase"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Erhöhung der Phasen"));

        Register("Waiting for phase reduction",
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for phase reduction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Reduzierung der Phasen"));

        Register("Waiting for charge stop",
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for charge stop"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Lade-Stopp"));

        Register("Configured max Soc is reached",
            new TextLocalizationTranslation(LanguageCodes.English, "Configured max Soc is reached"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurierte maximale SoC ist erreicht"));

        Register("Car side SOC limit is reached. To start charging, the car side SOC limit needs to be at least {0}% higher than the actual SOC.",
            new TextLocalizationTranslation(LanguageCodes.English, "Car side SOC limit is reached. To start charging, the car side SOC limit needs to be at least {0}% higher than the actual SOC."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugseitiges SoC-Limit ist erreicht. Um mit dem Laden zu beginnen, muss das Fahrzeuglimit mindestens {0}% höher als der aktuelle SoC sein."));

        Register("Charging stopped by car, e.g. it is full or its charge limit is reached.",
            new TextLocalizationTranslation(LanguageCodes.English, "Charging stopped by car, e.g. it is full or its charge limit is reached."),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden wurde vom Fahrzeug gestoppt, z. B. weil es voll ist oder das Ladeziel erreicht wurde."));

        Register("Waiting for charge start",
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for charge start"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Lade-Start"));

        Register("Charging stopped because of not enough max combined current.",
            new TextLocalizationTranslation(LanguageCodes.English, "Charging stopped because of not enough max combined current."),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden gestoppt, da nicht genug maximale kombinierte Stromstärke verfügbar ist."));

        Register("Charge mode is off or max SoC is reached.",
            new TextLocalizationTranslation(LanguageCodes.English, "Charge mode is off or max SoC is reached."),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus ist aus oder die maximale SoC ist erreicht."));

        Register("Min Phases or Max Phases is unkown. Check the logs for further details.",
            new TextLocalizationTranslation(LanguageCodes.English, "Min Phases or Max Phases is unkown. Check the logs for further details."),
            new TextLocalizationTranslation(LanguageCodes.German, "Minimale oder maximale Phasenanzahl ist unbekannt. Weitere Details stehen im Log."));

        Register("Estimated voltage while charging is unkown. Check the logs for further details.",
            new TextLocalizationTranslation(LanguageCodes.English, "Estimated voltage while charging is unkown. Check the logs for further details."),
            new TextLocalizationTranslation(LanguageCodes.German, "Geschätzte Spannung während des Ladens ist unbekannt. Weitere Details stehen im Log."));

        Register("Reserved {0}W for Home battery charging as its SOC ({1}%) is below minimum SOC ({2}%)",
            new TextLocalizationTranslation(LanguageCodes.English, "Reserved {0}W for Home battery charging as its SOC ({1}%) is below minimum SOC ({2}%)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Reserviere {0}W zum Laden der Hausbatterie, da ihr SoC ({1}%) unter dem Mindest-SoC ({2}%) liegt."));
    }
}
