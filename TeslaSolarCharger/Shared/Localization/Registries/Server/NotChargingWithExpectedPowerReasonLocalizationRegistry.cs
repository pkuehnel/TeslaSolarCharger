using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Shared.Localization.Registries.Server;

public class NotChargingWithExpectedPowerReasonLocalizationRegistry : TextLocalizationRegistry<NotChargingWithExpectedPowerReasonLocalizationRegistry>
{
    protected override void Configure()
    {
        Register(TranslationKeys.OcppConnectionNotEstablishedAfterA,
            new TextLocalizationTranslation(LanguageCodes.English, "OCPP connection not established. After a TSC or charger reboot it can take up to 5 minutes until the charger is connected again."),
            new TextLocalizationTranslation(LanguageCodes.German, "OCPP-Verbindung nicht hergestellt. Nach einem Neustart von TSC oder der Wallbox kann es bis zu 5 Minuten dauern, bis die Wallbox wieder verbunden ist."));

        Register(TranslationKeys.CarIsNotAtHome,
            new TextLocalizationTranslation(LanguageCodes.English, "Car is not at home"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto befindet sich nicht zu Hause"));

        Register(TranslationKeys.CarIsNotPluggedIn,
            new TextLocalizationTranslation(LanguageCodes.English, "Car is not plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "Auto ist nicht eingesteckt"));

        Register(TranslationKeys.ChargingConnectorIsNotPluggedIn,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging connector is not plugged in"),
            new TextLocalizationTranslation(LanguageCodes.German, "Ladeanschluss ist nicht eingesteckt"));

        Register(TranslationKeys.ChargingCanTStartBecauseThe,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging can’t start because the car isn’t allowing it. This may happen if the battery is already full, charging was stopped in the car or the app, the car is in standby or sleep mode, or has a delayed charging schedule."),
            new TextLocalizationTranslation(LanguageCodes.German, "Das Laden kann nicht gestartet werden, da das Auto es nicht zulässt. Mögliche Gründe sind, dass die Batterie bereits voll ist, das Laden im Auto oder in der App gestoppt wurde, das Auto im Standby- oder Ruhemodus ist oder eine Ladezeit geplant wurde."));

        Register(TranslationKeys.WaitingPhaseSwitchCooldownTimeBefore,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting phase switch cooldown time before starting to charge"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Abkühlzeit für Phasenumschaltung, bevor mit dem Laden begonnen wird"));

        Register(TranslationKeys.WaitingForPhaseIncrease,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for phase increase"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Erhöhung der Phasen"));

        Register(TranslationKeys.WaitingForPhaseReduction,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for phase reduction"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Reduzierung der Phasen"));

        Register(TranslationKeys.WaitingForChargeStop,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for charge stop"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Lade-Stopp"));

        Register(TranslationKeys.ConfiguredMaxSocIsReached,
            new TextLocalizationTranslation(LanguageCodes.English, "Configured max Soc is reached"),
            new TextLocalizationTranslation(LanguageCodes.German, "Konfigurierter maximaler Ladestand ist erreicht"));

        Register(TranslationKeys.CarSideSocLimitIsReached,
            new TextLocalizationTranslation(LanguageCodes.English, "Car side SOC limit is reached. To start charging, the car side SOC limit needs to be at least {0}% higher than the actual SOC."),
            new TextLocalizationTranslation(LanguageCodes.German, "Fahrzeugseitiges Ladelimit ist erreicht. Um mit dem Laden zu beginnen, muss das Fahrzeuglimit mindestens {0}% höher als der aktuelle Ladestand sein."));

        Register(TranslationKeys.ChargingStoppedByCarEG,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging stopped by car, e.g. it is full or its charge limit is reached."),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden wurde vom Fahrzeug gestoppt, z. B. weil es voll ist oder das Ladeziel erreicht wurde."));

        Register(TranslationKeys.WaitingForChargeStart,
            new TextLocalizationTranslation(LanguageCodes.English, "Waiting for charge start"),
            new TextLocalizationTranslation(LanguageCodes.German, "Warte auf Lade-Start"));

        Register(TranslationKeys.ChargingStoppedBecauseOfNotEnough,
            new TextLocalizationTranslation(LanguageCodes.English, "Charging stopped because of not enough max combined current."),
            new TextLocalizationTranslation(LanguageCodes.German, "Laden gestoppt, da nicht genug maximale kombinierte Stromstärke verfügbar ist."));

        Register(TranslationKeys.ChargeModeIsOffOrMax,
            new TextLocalizationTranslation(LanguageCodes.English, "Charge mode is off or max SoC is reached."),
            new TextLocalizationTranslation(LanguageCodes.German, "Lademodus ist aus oder der maximale Ladestand ist erreicht."));

        Register(TranslationKeys.MinPhasesOrMaxPhasesIs,
            new TextLocalizationTranslation(LanguageCodes.English, "Min Phases or Max Phases is unknown. Check the logs for further details."),
            new TextLocalizationTranslation(LanguageCodes.German, "Minimale oder maximale Phasenanzahl ist unbekannt. Weitere Details stehen im Log."));

        Register(TranslationKeys.EstimatedVoltageWhileChargingIsUnknown,
            new TextLocalizationTranslation(LanguageCodes.English, "Estimated voltage while charging is unknown. Check the logs for further details."),
            new TextLocalizationTranslation(LanguageCodes.German, "Geschätzte Spannung während des Ladens ist unbekannt. Weitere Details stehen im Log."));

        Register(TranslationKeys.Reserved0WForHomeBattery,
            new TextLocalizationTranslation(LanguageCodes.English, "Reserved {0}W for Home battery charging as its SOC ({1}%) is below minimum SOC ({2}%)"),
            new TextLocalizationTranslation(LanguageCodes.German, "Reserviere {0}W zum Laden der Hausbatterie, da ihr Ladestand ({1}%) unter dem Mindest-Ladestand ({2}%) liegt."));
    }
}
